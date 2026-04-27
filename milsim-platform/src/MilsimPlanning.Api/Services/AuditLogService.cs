using Microsoft.EntityFrameworkCore;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Models.AuditLogs;

namespace MilsimPlanning.Api.Services;

public class AuditLogService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public AuditLogService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    // ── GET /api/events/{eventId}/audit-logs ──────────────────────────────

    public async Task<(IReadOnlyList<FrequencyAuditLogDto> entries, int total)> GetAuditLogsAsync(
        Guid eventId,
        GetFrequencyAuditLogsRequest request)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        // Build query
        var query = _db.FrequencyAuditLogs
            .Include(a => a.User)
            .Where(a => a.EventId == eventId);

        // Apply filters (AC-07: filterable by unit or date range)
        if (!string.IsNullOrWhiteSpace(request.UnitName))
        {
            query = query.Where(a => a.UnitName.Contains(request.UnitName));
        }

        if (request.StartDate.HasValue)
        {
            query = query.Where(a => a.Timestamp >= request.StartDate);
        }

        if (request.EndDate.HasValue)
        {
            query = query.Where(a => a.Timestamp <= request.EndDate);
        }

        // Get total count before pagination
        var total = await query.CountAsync();

        // Apply sorting (AC-02: user configurable sort)
        query = request.SortBy.ToLowerInvariant() switch
        {
            "unitname" => request.SortOrder.ToLowerInvariant() == "asc"
                ? query.OrderBy(a => a.UnitName)
                : query.OrderByDescending(a => a.UnitName),
            "actiontype" => request.SortOrder.ToLowerInvariant() == "asc"
                ? query.OrderBy(a => a.ActionType)
                : query.OrderByDescending(a => a.ActionType),
            _ => request.SortOrder.ToLowerInvariant() == "asc"
                ? query.OrderBy(a => a.Timestamp)
                : query.OrderByDescending(a => a.Timestamp),
        };

        // Apply pagination (limit/offset pattern per PROJECT-CONTEXT)
        var entries = await query
            .Skip(request.Offset)
            .Take(request.Limit)
            .Select(a => new FrequencyAuditLogDto
            {
                Id = a.Id,
                EventId = a.EventId,
                Timestamp = a.Timestamp,
                UnitName = a.UnitName,
                UnitType = a.UnitType,
                PrimaryFrequency = a.PrimaryFrequency,
                AlternateFrequency = a.AlternateFrequency,
                ActionType = a.ActionType,
                UserName = a.User!.UserName ?? "Unknown",  // AC-03: user who performed action
                ConflictingUnitName = a.ConflictingUnitName,
            })
            .ToListAsync();

        return (entries, total);
    }

    // ── Create audit log entry (called by FrequencyService) ─────────────────

    public async Task LogFrequencyChangeAsync(
        Guid eventId,
        string unitName,
        string unitType,
        string? primaryFrequency,
        string? alternateFrequency,
        string actionType,
        string? conflictingUnitName = null)
    {
        var entry = new FrequencyAuditLog
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Timestamp = DateTime.UtcNow,  // AC-03: ISO-8601 timestamp (UTC)
            UnitName = unitName,
            UnitType = unitType,
            PrimaryFrequency = primaryFrequency,
            AlternateFrequency = alternateFrequency,
            ActionType = actionType,
            UserId = _currentUser.UserId,
            ConflictingUnitName = conflictingUnitName,
        };

        _db.FrequencyAuditLogs.Add(entry);
        // SaveChangesAsync called by caller (atomic transaction)
    }
}
