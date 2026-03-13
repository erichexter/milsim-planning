using System.Globalization;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Models.CsvImport;
using Microsoft.EntityFrameworkCore;

namespace MilsimPlanning.Api.Services;

public class RosterService
{
    private readonly AppDbContext _db;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;

    public RosterService(AppDbContext db, IEmailService emailService, IConfiguration config)
    {
        _db = db;
        _emailService = emailService;
        _config = config;
    }

    public async Task<CsvValidationResult> ValidateRosterCsvAsync(IFormFile file, Guid eventId)
    {
        var errors = new List<CsvRowError>();
        var seenEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // PITFALL 6: case-insensitive header matching
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            PrepareHeaderForMatch = args => args.Header.ToLowerInvariant()
        };

        using var reader = new StreamReader(file.OpenReadStream());
        using var csv = new CsvReader(reader, config);

        try
        {
            csv.Read();
            csv.ReadHeader();
        }
        catch (CsvHelperException ex)
        {
            return new CsvValidationResult
            {
                FatalError = $"CSV header could not be read: {ex.Message}"
            };
        }

        var rowNum = 1;
        var allRows = new List<(int Row, RosterImportRow? Data, string? ParseError)>();

        // PITFALL 1: manual row-by-row loop — collects ALL errors, not just first
        while (csv.Read())
        {
            rowNum++;
            try
            {
                var record = csv.GetRecord<RosterImportRow>();
                allRows.Add((rowNum, record, null));
            }
            catch (CsvHelperException ex)
            {
                allRows.Add((rowNum, null, ex.Message));
            }
        }

        var validCount = 0;
        foreach (var (row, data, parseError) in allRows)
        {
            var rowHasError = false;

            if (parseError is not null)
            {
                errors.Add(new CsvRowError(row, "parse", parseError, Severity.Error));
                continue;
            }

            // Email validation
            if (string.IsNullOrWhiteSpace(data!.Email) || !IsValidEmail(data.Email))
            {
                errors.Add(new CsvRowError(row, "email", "Invalid or missing email", Severity.Error));
                rowHasError = true;
            }
            else if (!seenEmails.Add(data.Email.ToLowerInvariant()))
            {
                errors.Add(new CsvRowError(row, "email", "Duplicate email in this CSV", Severity.Error));
                rowHasError = true;
            }

            // Name validation
            if (string.IsNullOrWhiteSpace(data.Name))
            {
                errors.Add(new CsvRowError(row, "name", "Name is required", Severity.Error));
                rowHasError = true;
            }

            // Callsign — warning only (ROST-03: errors block, warnings do not)
            if (string.IsNullOrWhiteSpace(data.Callsign))
                errors.Add(new CsvRowError(row, "callsign", "Callsign is missing", Severity.Warning));

            if (!rowHasError) validCount++;
        }

        return new CsvValidationResult
        {
            ValidCount = validCount,
            ErrorCount = errors.Count(e => e.Severity == Severity.Error),
            WarningCount = errors.Count(e => e.Severity == Severity.Warning),
            Errors = errors
        };
    }

    public async Task CommitRosterCsvAsync(IFormFile file, Guid eventId)
    {
        // Defense-in-depth: re-validate before commit
        var validation = await ValidateRosterCsvAsync(file, eventId);
        if (validation.FatalError is not null || validation.ErrorCount > 0)
            throw new RosterValidationException($"CSV has {validation.ErrorCount} errors");

        // Re-parse (IFormFile.OpenReadStream() returns a fresh stream each call)
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            PrepareHeaderForMatch = args => args.Header.ToLowerInvariant()
        };

        using var reader = new StreamReader(file.OpenReadStream());
        using var csv = new CsvReader(reader, config);
        csv.Read();
        csv.ReadHeader();

        var importedRows = new List<RosterImportRow>();
        while (csv.Read())
        {
            try { importedRows.Add(csv.GetRecord<RosterImportRow>()); }
            catch { /* skip — validated above */ }
        }

        var newlyAddedPlayers = new List<EventPlayer>();

        foreach (var row in importedRows.Where(r => !string.IsNullOrWhiteSpace(r.Email)))
        {
            var emailNormalized = row.Email.ToLowerInvariant();

            // PITFALL 4: use normalized email for lookup
            var existing = await _db.EventPlayers
                .FirstOrDefaultAsync(ep => ep.EventId == eventId && ep.Email == emailNormalized);

            if (existing is null)
            {
                var newPlayer = new EventPlayer
                {
                    EventId = eventId,
                    Email = emailNormalized,  // PITFALL 4: store as lowercase
                    Name = row.Name,
                    Callsign = row.Callsign,
                    TeamAffiliation = row.TeamAffiliation
                    // UserId = null (player hasn't registered yet)
                    // PlatoonId/SquadId = null (no assignment yet)
                };
                _db.EventPlayers.Add(newPlayer);
                newlyAddedPlayers.Add(newPlayer);
            }
            else
            {
                // PITFALL 2: update ONLY CSV fields — never touch PlatoonId/SquadId
                existing.Name = row.Name;
                existing.Callsign = row.Callsign;
                existing.TeamAffiliation = row.TeamAffiliation;
                // existing.PlatoonId — UNTOUCHED
                // existing.SquadId   — UNTOUCHED
            }
        }

        await _db.SaveChangesAsync();

        // ROST-06: invite new unregistered players after commit
        var unregisteredNew = newlyAddedPlayers.Where(p => p.UserId is null).ToList();
        await SendInvitationsAsync(unregisteredNew, eventId);
    }

    private async Task SendInvitationsAsync(List<EventPlayer> players, Guid eventId)
    {
        if (!players.Any()) return;

        var appUrl = _config["AppUrl"] ?? "https://localhost";

        async Task SendOne(EventPlayer player)
        {
            var inviteLink = $"{appUrl}/invite?eventId={eventId}&email={Uri.EscapeDataString(player.Email)}";
            await _emailService.SendAsync(
                player.Email,
                "You've been invited to an event",
                $"<p>Hi {player.Name},</p><p>You have been added to an event roster. <a href='{inviteLink}'>Click here to set up your account.</a></p>"
            );
        }

        if (players.Count <= 20)
        {
            // Synchronous for small batches (common case — ROST-06)
            foreach (var player in players)
                await SendOne(player);
        }
        else
        {
            // Large batch: send synchronously as fallback (Phase 3 adds proper async blast)
            // If IBackgroundTaskQueue is available from Phase 1, queue here instead
            foreach (var player in players)
                await SendOne(player);
        }
    }

    private static bool IsValidEmail(string email)
        => Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase);
}

/// <summary>
/// Thrown by RosterService.CommitRosterCsvAsync when the CSV contains validation errors.
/// RosterController maps this to HTTP 422 Unprocessable Entity.
/// </summary>
public class RosterValidationException : Exception
{
    public RosterValidationException(string message) : base(message) { }
}
