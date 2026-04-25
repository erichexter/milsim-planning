using Microsoft.EntityFrameworkCore;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Models.Channels;

namespace MilsimPlanning.Api.Services;

/// <summary>
/// Channel CRUD for Story 4 endpoints — returns new DTOs from Models/Channels/.
/// Complements the existing RadioChannelService (which returns Models/RadioChannels/ DTOs).
/// </summary>
public class RadioChannelChannelService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public RadioChannelChannelService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    // ── GET /api/events/{eventId}/radio-channels ─────────────────────────────

    public async Task<List<RadioChannelListDto>> ListAsync(Guid eventId)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var channels = await _db.RadioChannels
            .Include(rc => rc.Assignments)
            .Where(rc => rc.EventId == eventId && !rc.IsDeleted)
            .OrderBy(rc => rc.Order)
            .ToListAsync();

        return channels.Select(rc => new RadioChannelListDto(
            rc.Id,
            rc.Name,
            rc.CallSign,
            rc.Scope.ToString(),
            rc.Assignments.Count,
            rc.Assignments.Count(a => a.HasConflict)
        )).ToList();
    }

    // ── POST /api/events/{eventId}/radio-channels ─────────────────────────────

    public async Task<RadioChannelDetailDto> CreateAsync(Guid eventId, CreateRadioChannelRequest request)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var evt = await _db.Events
            .Include(e => e.Faction)
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found.");

        AssertCommanderAccess(evt.Faction);

        var scope = ParseScope(request.Scope);

        // Validate uniqueness within event
        var duplicate = await _db.RadioChannels
            .AnyAsync(rc => rc.EventId == eventId && rc.Name == request.Name.Trim() && !rc.IsDeleted);

        if (duplicate)
            throw new InvalidOperationException("Channel name already exists in this operation.");

        var order = await _db.RadioChannels
            .Where(rc => rc.EventId == eventId && !rc.IsDeleted)
            .Select(rc => (int?)rc.Order)
            .MaxAsync() ?? -1;

        var now = DateTime.UtcNow;

        var channel = new RadioChannel
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Name = request.Name.Trim(),
            CallSign = request.CallSign?.Trim(),
            Scope = scope,
            Order = order + 1,
            IsDeleted = false
        };

        _db.RadioChannels.Add(channel);
        await _db.SaveChangesAsync();

        return new RadioChannelDetailDto(
            channel.Id,
            channel.EventId,
            channel.Name,
            channel.CallSign,
            channel.Scope.ToString(),
            [],
            now
        );
    }

    // ── PATCH /api/radio-channels/{channelId} ─────────────────────────────────

    public async Task<RadioChannelDetailDto> UpdateAsync(Guid channelId, UpdateRadioChannelRequest request)
    {
        var channel = await _db.RadioChannels
            .Include(rc => rc.Event)
                .ThenInclude(e => e.Faction)
            .Include(rc => rc.Assignments)
            .FirstOrDefaultAsync(rc => rc.Id == channelId && !rc.IsDeleted)
            ?? throw new KeyNotFoundException($"Channel {channelId} not found.");

        ScopeGuard.AssertEventAccess(_currentUser, channel.EventId);
        AssertCommanderAccess(channel.Event.Faction);

        var scope = ParseScope(request.Scope);
        var trimmedName = request.Name.Trim();

        // Validate uniqueness within event (exclude self)
        var duplicate = await _db.RadioChannels
            .AnyAsync(rc => rc.EventId == channel.EventId
                         && rc.Name == trimmedName
                         && rc.Id != channelId
                         && !rc.IsDeleted);

        if (duplicate)
            throw new InvalidOperationException("Channel name already exists in this operation.");

        channel.Name = trimmedName;
        channel.Scope = scope;
        await _db.SaveChangesAsync();

        return new RadioChannelDetailDto(
            channel.Id,
            channel.EventId,
            channel.Name,
            channel.CallSign,
            channel.Scope.ToString(),
            [],
            DateTime.UtcNow
        );
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void AssertCommanderAccess(Faction faction)
    {
        if (faction.CommanderId != _currentUser.UserId)
            throw new ForbiddenException("User is not the commander of this event's faction");
    }

    private static ChannelScope ParseScope(string scope) =>
        scope.ToUpperInvariant() switch
        {
            "VHF" => ChannelScope.VHF,
            "UHF" => ChannelScope.UHF,
            _ => throw new ArgumentException($"Invalid scope '{scope}'. Must be 'VHF' or 'UHF'.")
        };
}
