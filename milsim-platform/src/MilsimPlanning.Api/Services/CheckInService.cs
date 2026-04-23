using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Models.CheckIn;
using Microsoft.EntityFrameworkCore;

namespace MilsimPlanning.Api.Services;

/// <summary>
/// Service for handling participant check-in at kiosk events.
/// Validates QR codes, records check-in events, and prevents duplicate check-ins.
/// </summary>
public class CheckInService
{
    private readonly AppDbContext _db;
    private readonly QrValidationService _qrValidation;

    public CheckInService(AppDbContext db, QrValidationService qrValidation)
    {
        _db = db;
        _qrValidation = qrValidation;
    }

    /// <summary>
    /// Records a check-in for a participant by QR code scan.
    /// Validates the QR code and checks for duplicate entries.
    /// </summary>
    public async Task<CheckInRecordDto> RecordCheckInAsync(
        Guid eventId,
        string qrCodeValue,
        string? kioskId = null)
    {
        // Validate event exists
        var evt = await _db.Events
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found");

        // Validate QR code format
        await _qrValidation.ValidateQrCodeAsync(qrCodeValue);

        // Parse QR code to get participant ID (simple format: "{participantId}")
        if (!Guid.TryParse(qrCodeValue, out var participantId))
            throw new ArgumentException("QR code does not contain a valid participant ID");

        // Check participant exists
        var participant = await _db.EventPlayers
            .Include(p => p.Squad)
            .ThenInclude(s => s.Platoon)
            .ThenInclude(p => p.Faction)
            .FirstOrDefaultAsync(p => p.Id == participantId && p.EventId == eventId)
            ?? throw new KeyNotFoundException($"Participant {participantId} not found for event {eventId}");

        // Check for duplicate check-in (unique constraint on (EventId, ParticipantId))
        var existingCheckIn = await _db.EventParticipantCheckIns
            .FirstOrDefaultAsync(c => c.EventId == eventId && c.ParticipantId == participantId);

        if (existingCheckIn is not null)
            throw new InvalidOperationException($"Participant {participantId} already checked in for event {eventId}");

        // Record check-in
        var checkIn = new EventParticipantCheckIn
        {
            EventId = eventId,
            ParticipantId = participantId,
            QrCodeValue = qrCodeValue,
            ScannedAtUtc = DateTime.UtcNow,
            KioskId = kioskId,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.EventParticipantCheckIns.Add(checkIn);
        await _db.SaveChangesAsync();

        // Return DTO with participant information
        var faction = participant.Squad?.Platoon?.Faction?.Name ?? "Unknown";
        return new CheckInRecordDto(
            ParticipantId: participant.Id,
            Name: participant.Name,
            Faction: faction,
            ScannedAtUtc: checkIn.ScannedAtUtc
        );
    }
}
