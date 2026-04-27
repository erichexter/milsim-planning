using Microsoft.EntityFrameworkCore;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Models.Channels;

namespace MilsimPlanning.Api.Services;

/// <summary>
/// Queries and manages the audit log for frequency assignments (Story 7).
/// Provides access to chronological audit trail of all frequency assignment operations.
/// </summary>
public class FrequencyAuditLogService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public FrequencyAuditLogService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Get audit log entries for an event.
    /// AC-02: chronological entries (newest first by default)
    /// AC-07: supports optional unit filter and date range
    /// </summary>
    public async Task<List<FrequencyAuditLogDto>> GetAuditLogAsync(
        Guid eventId,
        string? unitFilter = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        bool newestFirst = true)
    {
        // Verify event access
        var eventExists = await _db.Events
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found.");

        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var query = _db.FrequencyAuditLogs
            .Where(al => al.EventId == eventId);

        // AC-07: Filter by unit name if provided
        if (!string.IsNullOrWhiteSpace(unitFilter))
        {
            query = query.Where(al => al.UnitName.Contains(unitFilter));
        }

        // AC-07: Filter by date range if provided
        if (startDate.HasValue)
        {
            query = query.Where(al => al.OccurredAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(al => al.OccurredAt <= endDate.Value);
        }

        // AC-02: Sort chronologically (newest first or oldest first)
        if (newestFirst)
        {
            query = query.OrderByDescending(al => al.OccurredAt);
        }
        else
        {
            query = query.OrderBy(al => al.OccurredAt);
        }

        var logs = await query.ToListAsync();
        return logs.Select(ToDto).ToList();
    }

    /// <summary>
    /// Write an audit log entry for a frequency assignment action.
    /// Called by RadioChannelAssignmentService when creating, updating, or deleting assignments.
    /// </summary>
    public async Task LogAssignmentActionAsync(
        Guid eventId,
        string channelName,       // AC-03: channel name
        string unitType,
        Guid unitId,
        string unitName,
        string? primaryFrequency,
        string? alternateFrequency,
        string actionType,
        string? conflictingUnitName = null)
    {
        // Get user's display name from profile (or fall back to UserId)
        var userProfile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == _currentUser.UserId);
        var displayName = userProfile?.DisplayName ?? _currentUser.UserId;

        var log = new FrequencyAuditLog
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            ChannelName = channelName,
            UnitType = unitType,
            UnitId = unitId,
            UnitName = unitName,
            PrimaryFrequency = primaryFrequency,
            AlternateFrequency = alternateFrequency,
            ActionType = actionType,
            ConflictingUnitName = conflictingUnitName,
            PerformedByUserId = _currentUser.UserId,
            PerformedByDisplayName = displayName,
            OccurredAt = DateTime.UtcNow
        };

        _db.FrequencyAuditLogs.Add(log);
        await _db.SaveChangesAsync();
    }

    private static FrequencyAuditLogDto ToDto(FrequencyAuditLog log)
    {
        return new FrequencyAuditLogDto(
            log.Id,
            log.EventId,
            log.ChannelName,
            log.UnitType,
            log.UnitId,
            log.UnitName,
            log.PrimaryFrequency,
            log.AlternateFrequency,
            log.ActionType,
            log.ConflictingUnitName,
            log.PerformedByUserId,
            log.PerformedByDisplayName,
            log.OccurredAt
        );
    }
}
