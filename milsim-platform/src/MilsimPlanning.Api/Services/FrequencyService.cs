using Microsoft.EntityFrameworkCore;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Domain;
using MilsimPlanning.Api.Models.Frequencies;

namespace MilsimPlanning.Api.Services;

public class FrequencyService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly NatoFrequencyValidationService _validator;

    public FrequencyService(AppDbContext db, ICurrentUser currentUser, NatoFrequencyValidationService validator)
    {
        _db = db;
        _currentUser = currentUser;
        _validator = validator;
    }

    // ── GET /api/events/{eventId}/frequencies ─────────────────────────────────

    public async Task<EventFrequenciesDto> GetEventFrequenciesAsync(Guid eventId)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        // Use per-event membership role, not the global JWT role, to determine visibility tier
        var membership = await _db.EventMemberships
            .FirstOrDefaultAsync(m => m.EventId == eventId && m.UserId == _currentUser.UserId);
        var role = membership?.Role ?? AppRoles.Player;

        // Load the current user's event player record for assignment context
        var eventPlayer = await _db.EventPlayers
            .Include(ep => ep.Squad)
            .Include(ep => ep.Platoon)
                .ThenInclude(p => p!.Faction)
            .FirstOrDefaultAsync(ep => ep.EventId == eventId && ep.UserId == _currentUser.UserId);

        FrequencyLevelDto? squadLevel = null;
        FrequencyLevelDto? platoonLevel = null;
        FrequencyLevelDto? commandLevel = null;

        if (role == AppRoles.Player)
        {
            // Squad member: sees squad level only
            if (eventPlayer?.Squad != null)
                squadLevel = new FrequencyLevelDto(eventPlayer.Squad.PrimaryFrequency, eventPlayer.Squad.BackupFrequency);
        }
        else if (role == AppRoles.SquadLeader)
        {
            // Squad leader: sees squad + platoon
            if (eventPlayer?.Squad != null)
                squadLevel = new FrequencyLevelDto(eventPlayer.Squad.PrimaryFrequency, eventPlayer.Squad.BackupFrequency);

            if (eventPlayer?.Squad?.Platoon != null)
                platoonLevel = new FrequencyLevelDto(eventPlayer.Squad.Platoon.PrimaryFrequency, eventPlayer.Squad.Platoon.BackupFrequency);
            else if (eventPlayer?.Platoon != null)
                platoonLevel = new FrequencyLevelDto(eventPlayer.Platoon.PrimaryFrequency, eventPlayer.Platoon.BackupFrequency);
        }
        else if (role == AppRoles.PlatoonLeader)
        {
            // Platoon leader: sees platoon + command (faction)
            var platoon = eventPlayer?.Platoon
                ?? (eventPlayer?.Squad != null
                    ? await _db.Platoons.Include(p => p.Faction).FirstOrDefaultAsync(p => p.Id == eventPlayer.Squad.PlatoonId)
                    : null);

            if (platoon != null)
            {
                platoonLevel = new FrequencyLevelDto(platoon.PrimaryFrequency, platoon.BackupFrequency);

                var faction = platoon.Faction
                    ?? await _db.Factions.FirstOrDefaultAsync(f => f.Id == platoon.FactionId);

                if (faction != null)
                    commandLevel = new FrequencyLevelDto(faction.PrimaryFrequency, faction.BackupFrequency);
            }
        }
        else if (role == AppRoles.FactionCommander || role == AppRoles.SystemAdmin)
        {
            // Faction commander: all available levels
            var faction = await _db.Factions
                .FirstOrDefaultAsync(f => f.EventId == eventId);

            if (faction != null)
            {
                commandLevel = new FrequencyLevelDto(faction.PrimaryFrequency, faction.BackupFrequency);

                if (eventPlayer?.Squad != null)
                    squadLevel = new FrequencyLevelDto(eventPlayer.Squad.PrimaryFrequency, eventPlayer.Squad.BackupFrequency);

                if (eventPlayer?.Platoon != null)
                    platoonLevel = new FrequencyLevelDto(eventPlayer.Platoon.PrimaryFrequency, eventPlayer.Platoon.BackupFrequency);
                else if (eventPlayer?.Squad?.Platoon != null)
                    platoonLevel = new FrequencyLevelDto(eventPlayer.Squad.Platoon.PrimaryFrequency, eventPlayer.Squad.Platoon.BackupFrequency);
            }
        }

        return new EventFrequenciesDto(squadLevel, platoonLevel, commandLevel);
    }

    // ── PUT /api/squads/{squadId}/frequencies ─────────────────────────────────

    public async Task<FrequencyLevelDto> SetSquadFrequenciesAsync(Guid squadId, SetFrequenciesRequest request)
    {
        var squad = await _db.Squads
            .Include(s => s.Platoon)
                .ThenInclude(p => p.Faction)
            .FirstOrDefaultAsync(s => s.Id == squadId)
            ?? throw new KeyNotFoundException($"Squad {squadId} not found");

        AssertCommanderAccess(squad.Platoon.Faction);

        // Validate frequencies against NATO ranges and spacing
        ValidateFrequencies(request.PrimaryFrequency, request.BackupFrequency);

        squad.PrimaryFrequency = request.PrimaryFrequency;
        squad.BackupFrequency = request.BackupFrequency;
        await _db.SaveChangesAsync();

        return new FrequencyLevelDto(squad.PrimaryFrequency, squad.BackupFrequency);
    }

    // ── PUT /api/platoons/{platoonId}/frequencies ─────────────────────────────

    public async Task<FrequencyLevelDto> SetPlatoonFrequenciesAsync(Guid platoonId, SetFrequenciesRequest request)
    {
        var platoon = await _db.Platoons
            .Include(p => p.Faction)
            .FirstOrDefaultAsync(p => p.Id == platoonId)
            ?? throw new KeyNotFoundException($"Platoon {platoonId} not found");

        AssertCommanderAccess(platoon.Faction);

        // Validate frequencies against NATO ranges and spacing
        ValidateFrequencies(request.PrimaryFrequency, request.BackupFrequency);

        platoon.PrimaryFrequency = request.PrimaryFrequency;
        platoon.BackupFrequency = request.BackupFrequency;
        await _db.SaveChangesAsync();

        return new FrequencyLevelDto(platoon.PrimaryFrequency, platoon.BackupFrequency);
    }

    // ── PUT /api/factions/{factionId}/frequencies ─────────────────────────────

    public async Task<FrequencyLevelDto> SetFactionFrequenciesAsync(Guid factionId, SetFrequenciesRequest request)
    {
        var faction = await _db.Factions
            .FirstOrDefaultAsync(f => f.Id == factionId)
            ?? throw new KeyNotFoundException($"Faction {factionId} not found");

        AssertCommanderAccess(faction);

        // Validate frequencies against NATO ranges and spacing
        ValidateFrequencies(request.PrimaryFrequency, request.BackupFrequency);

        faction.PrimaryFrequency = request.PrimaryFrequency;
        faction.BackupFrequency = request.BackupFrequency;
        await _db.SaveChangesAsync();

        return new FrequencyLevelDto(faction.PrimaryFrequency, faction.BackupFrequency);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Validates both primary and backup frequencies against NATO ranges and 25 kHz spacing.
    /// Frequencies must be within VHF (30.0–87.975 MHz) or UHF (225–400 MHz) bands.
    /// Null frequencies are allowed.
    /// </summary>
    private void ValidateFrequencies(string? primaryFrequency, string? backupFrequency)
    {
        // Validate primary frequency if provided
        if (!string.IsNullOrWhiteSpace(primaryFrequency))
        {
            if (!decimal.TryParse(primaryFrequency, out var primaryFreq))
                throw new ArgumentException($"Primary frequency '{primaryFrequency}' is not a valid decimal value.");

            var scope = InferChannelScope(primaryFreq);
            _validator.Validate(primaryFreq, scope);
        }

        // Validate backup frequency if provided
        if (!string.IsNullOrWhiteSpace(backupFrequency))
        {
            if (!decimal.TryParse(backupFrequency, out var backupFreq))
                throw new ArgumentException($"Backup frequency '{backupFrequency}' is not a valid decimal value.");

            var scope = InferChannelScope(backupFreq);
            _validator.Validate(backupFreq, scope);
        }
    }

    /// <summary>
    /// Infers the NATO channel scope (VHF or UHF) from a frequency value in MHz.
    /// VHF: 30.0–87.975 MHz
    /// UHF: 225.0–400.0 MHz
    /// Throws if frequency is outside both ranges.
    /// </summary>
    private ChannelScope InferChannelScope(decimal frequency)
    {
        const decimal VhfMin = 30.000m;
        const decimal VhfMax = 87.975m;
        const decimal UhfMin = 225.000m;
        const decimal UhfMax = 400.000m;

        if (frequency >= VhfMin && frequency <= VhfMax)
            return ChannelScope.VHF;

        if (frequency >= UhfMin && frequency <= UhfMax)
            return ChannelScope.UHF;

        // Frequency is in the gap or outside both bands
        throw new ArgumentException(
            $"Frequency {frequency} MHz is not within any NATO band. " +
            "VHF must be 30.0–87.975 MHz; UHF must be 225–400 MHz.");
    }

    private void AssertCommanderAccess(Data.Entities.Faction faction)
    {
        if (faction.CommanderId != _currentUser.UserId)
            throw new ForbiddenException("User is not the commander of this event's faction");
    }
}
