using Microsoft.EntityFrameworkCore;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Models.Channels;

namespace MilsimPlanning.Api.Services;

/// <summary>
/// Service for exporting channel-unit frequency mappings as JSON
/// </summary>
public class FrequencyExportService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public FrequencyExportService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Generates frequency mapping export for an operation (event)
    /// </summary>
    public async Task<FrequencyMappingExportDto> ExportAsync(Guid eventId)
    {
        // Verify user has access to this event
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        // Get event with all radio channels
        var evt = await _db.Events
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found.");

        // Get all channels for this event with their assignments
        var channels = await _db.RadioChannels
            .Where(rc => rc.EventId == eventId && !rc.IsDeleted)
            .OrderBy(rc => rc.Order)
            .Include(rc => rc.Assignments)
                .ThenInclude(a => a.Squad)
            .Include(rc => rc.Assignments)
                .ThenInclude(a => a.Platoon)
            .Include(rc => rc.Assignments)
                .ThenInclude(a => a.Faction)
            .ToListAsync();

        // Build the export DTO
        var exportTimestamp = DateTime.UtcNow.ToString("o"); // ISO 8601 format

        var channelExports = channels.Select(channel => new ChannelFrequencyExportDto(
            Name: channel.Name,
            Scope: channel.Scope.ToString(),
            Assignments: BuildAssignmentsList(channel.Assignments)
        )).ToList();

        return new FrequencyMappingExportDto(
            OperationName: evt.Name,
            ExportTimestamp: exportTimestamp,
            Channels: channelExports
        );
    }

    /// <summary>
    /// Builds the list of frequency assignments for a channel
    /// Maps polymorphic unit references to unit identifiers
    /// </summary>
    private static List<FrequencyAssignmentExportDto> BuildAssignmentsList(
        ICollection<RadioChannelAssignment> assignments)
    {
        return assignments
            .Where(a => a.Primary.HasValue) // Only export assignments with at least primary frequency
            .Select(a => new FrequencyAssignmentExportDto(
                Unit: GetUnitIdentifier(a),
                PrimaryFrequency: a.Primary,
                AlternateFrequency: a.Alternate
            ))
            .ToList();
    }

    /// <summary>
    /// Generates a unit identifier string from polymorphic unit reference
    /// e.g., "Squad-Alpha", "Platoon-Bravo", "Faction-Command"
    /// </summary>
    private static string GetUnitIdentifier(RadioChannelAssignment assignment)
    {
        if (assignment.Squad != null)
            return $"Squad-{assignment.Squad.Name}";

        if (assignment.Platoon != null)
            return $"Platoon-{assignment.Platoon.Name}";

        if (assignment.Faction != null)
            return $"Faction-{assignment.Faction.Name}";

        // Fallback (should not happen in normal operation)
        return $"Unknown-{assignment.Id:N}";
    }
}
