using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Infrastructure.BackgroundJobs;
using MilsimPlanning.Api.Models.Hierarchy;
using Microsoft.EntityFrameworkCore;

namespace MilsimPlanning.Api.Services;

public class HierarchyService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly INotificationQueue _notificationQueue;

    public HierarchyService(AppDbContext db, ICurrentUser currentUser, INotificationQueue notificationQueue)
    {
        _db = db;
        _currentUser = currentUser;
        _notificationQueue = notificationQueue;
    }

    // ── HIER-01: Create Platoon ───────────────────────────────────────────────

    public async Task<Platoon> CreatePlatoonAsync(Guid eventId, string name, bool isCommandElement = false)
    {
        var faction = await _db.Factions
            .FirstOrDefaultAsync(f => f.EventId == eventId)
            ?? throw new KeyNotFoundException($"Faction for event {eventId} not found");

        AssertCommanderAccess(faction);

        var maxOrder = await _db.Platoons
            .Where(p => p.FactionId == faction.Id)
            .Select(p => (int?)p.Order)
            .MaxAsync() ?? 0;

        var platoon = new Platoon
        {
            FactionId = faction.Id,
            Name = name,
            Order = maxOrder + 1,
            IsCommandElement = isCommandElement
        };
        _db.Platoons.Add(platoon);
        await _db.SaveChangesAsync();
        return platoon;
    }

    // ── HIER-02: Create Squad within Platoon ──────────────────────────────────

    public async Task<Squad> CreateSquadAsync(Guid platoonId, string name)
    {
        var platoon = await _db.Platoons
            .Include(p => p.Faction)
            .FirstOrDefaultAsync(p => p.Id == platoonId)
            ?? throw new KeyNotFoundException($"Platoon {platoonId} not found");

        AssertCommanderAccess(platoon.Faction);

        var maxOrder = await _db.Squads
            .Where(s => s.PlatoonId == platoonId)
            .Select(s => (int?)s.Order)
            .MaxAsync() ?? 0;

        var squad = new Squad
        {
            PlatoonId = platoonId,
            Name = name,
            Order = maxOrder + 1
        };
        _db.Squads.Add(squad);
        await _db.SaveChangesAsync();
        return squad;
    }

    // ── Set player role label ─────────────────────────────────────────────────

    public async Task SetPlayerRoleAsync(Guid eventPlayerId, string? role)
    {
        var player = await _db.EventPlayers
            .Include(ep => ep.Event)
                .ThenInclude(e => e.Faction)
            .FirstOrDefaultAsync(ep => ep.Id == eventPlayerId)
            ?? throw new KeyNotFoundException($"EventPlayer {eventPlayerId} not found");

        AssertCommanderAccess(player.Event.Faction);

        player.Role = string.IsNullOrWhiteSpace(role) ? null : role.Trim();
        await _db.SaveChangesAsync();
    }

    // ── HIER-03 / HIER-04 / HIER-05: Assign/move player to squad ─────────────

    public async Task AssignSquadAsync(Guid eventPlayerId, Guid? squadId)
    {
        var player = await _db.EventPlayers
            .Include(ep => ep.Event)
                .ThenInclude(e => e.Faction)
            .Include(ep => ep.Squad)
                .ThenInclude(s => s!.Platoon)
            .FirstOrDefaultAsync(ep => ep.Id == eventPlayerId)
            ?? throw new KeyNotFoundException($"EventPlayer {eventPlayerId} not found");

        AssertCommanderAccess(player.Event.Faction);

        var oldSquadName = player.Squad?.Name ?? "(unassigned)";
        var oldPlatoonName = player.Squad?.Platoon?.Name ?? "(unassigned)";

        // IDOR protection: if assigning to a squad, verify it belongs to the same event
        if (squadId.HasValue)
        {
            var squadBelongsToEvent = await _db.Squads
                .AnyAsync(s => s.Id == squadId.Value
                    && s.Platoon.Faction.EventId == player.EventId);

            if (!squadBelongsToEvent)
                throw new ForbiddenException($"Squad {squadId} does not belong to event {player.EventId}");
        }

        player.SquadId = squadId;
        if (squadId.HasValue)
        {
            var squad = await _db.Squads.FindAsync(squadId.Value);
            player.PlatoonId = squad?.PlatoonId;
        }
        else
        {
            // Unassign: clear both squad and platoon
            player.PlatoonId = null;
        }

        await _db.SaveChangesAsync();

        Squad? newSquad = null;
        if (squadId.HasValue)
        {
            newSquad = await _db.Squads
                .Include(s => s.Platoon)
                .FirstOrDefaultAsync(s => s.Id == squadId.Value);
        }

        if (player.UserId is not null && !string.IsNullOrWhiteSpace(player.Email))
        {
            var newSquadName = newSquad?.Name ?? "(unassigned)";
            var newPlatoonName = newSquad?.Platoon?.Name ?? "(unassigned)";

            await _notificationQueue.EnqueueAsync(new SquadChangeJob(
                player.Email,
                player.Name,
                oldPlatoonName,
                oldSquadName,
                newPlatoonName,
                newSquadName
            ));
        }
    }

    // ── Assign player directly to a platoon (HQ / command element slot) ─────

    public async Task AssignToPlatoonAsync(Guid eventPlayerId, Guid? platoonId)
    {
        var player = await _db.EventPlayers
            .Include(ep => ep.Event)
                .ThenInclude(e => e.Faction)
            .FirstOrDefaultAsync(ep => ep.Id == eventPlayerId)
            ?? throw new KeyNotFoundException($"EventPlayer {eventPlayerId} not found");

        AssertCommanderAccess(player.Event.Faction);

        // IDOR protection: verify platoon belongs to the same event
        if (platoonId.HasValue)
        {
            var platoonBelongsToEvent = await _db.Platoons
                .AnyAsync(p => p.Id == platoonId.Value && p.Faction.EventId == player.EventId);

            if (!platoonBelongsToEvent)
                throw new ForbiddenException($"Platoon {platoonId} does not belong to event {player.EventId}");
        }

        // Set platoon directly; always clear squad (platoon-level assignment has no squad)
        player.PlatoonId = platoonId;
        player.SquadId = null;

        await _db.SaveChangesAsync();
    }

    // ── Bulk assign players to a squad or platoon slot ───────────────────────
    // destination encoding: "squad:{id}" | "platoon:{id}"

    public async Task BulkAssignAsync(Guid eventId, IEnumerable<Guid> playerIds, string destination)
    {
        // Verify the destination belongs to this event before touching any players
        if (destination.StartsWith("squad:"))
        {
            var squadId = Guid.Parse(destination["squad:".Length..]);
            var squadBelongsToEvent = await _db.Squads
                .AnyAsync(s => s.Id == squadId && s.Platoon.Faction.EventId == eventId);
            if (!squadBelongsToEvent)
                throw new ForbiddenException($"Squad {squadId} does not belong to event {eventId}");

            foreach (var playerId in playerIds)
                await AssignSquadAsync(playerId, squadId);
        }
        else if (destination.StartsWith("platoon:"))
        {
            var platoonId = Guid.Parse(destination["platoon:".Length..]);
            var platoonBelongsToEvent = await _db.Platoons
                .AnyAsync(p => p.Id == platoonId && p.Faction.EventId == eventId);
            if (!platoonBelongsToEvent)
                throw new ForbiddenException($"Platoon {platoonId} does not belong to event {eventId}");

            foreach (var playerId in playerIds)
                await AssignToPlatoonAsync(playerId, platoonId);
        }
        else
        {
            throw new ArgumentException($"Unknown destination format: {destination}");
        }
    }

    // ── HIER-06: Get Roster Hierarchy (accessible to all faction members) ─────

    public async Task<RosterHierarchyDto> GetRosterHierarchyAsync(Guid eventId)
    {
        // HIER-06: accessible to player role (anyone with EventMembership in this event)
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var platoons = await _db.Platoons
            .Where(p => p.Faction.EventId == eventId)
            .Include(p => p.Squads)
            .OrderBy(p => p.Order)
            .ToListAsync();

        var players = await _db.EventPlayers
            .Where(ep => ep.EventId == eventId)
            .ToListAsync();

        // Build a lookup of profile callsigns for players who have logged in.
        // Profile callsign takes precedence over the roster CSV callsign.
        var linkedUserIds = players
            .Where(ep => ep.UserId is not null)
            .Select(ep => ep.UserId!)
            .ToHashSet();

        var profileCallsigns = await _db.UserProfiles
            .Where(p => linkedUserIds.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId, p => p.Callsign);

        string? EffectiveCallsign(EventPlayer ep) =>
            ep.UserId is not null && profileCallsigns.TryGetValue(ep.UserId, out var c) && !string.IsNullOrWhiteSpace(c)
                ? c
                : ep.Callsign;

        var platoonDtos = platoons.Select(p => new PlatoonDto(
            p.Id,
            p.Name,
            p.IsCommandElement,
            // HQ players: assigned to this platoon but not to any squad
            players
                .Where(ep => ep.PlatoonId == p.Id && ep.SquadId is null)
                .Select(ep => new PlayerDto(ep.Id, ep.Name, EffectiveCallsign(ep), ep.TeamAffiliation, ep.Role))
                .ToList(),
            p.Squads.OrderBy(s => s.Order).Select(s => new SquadDto(
                s.Id,
                s.Name,
                players
                    .Where(ep => ep.SquadId == s.Id)
                    .Select(ep => new PlayerDto(ep.Id, ep.Name, EffectiveCallsign(ep), ep.TeamAffiliation, ep.Role))
                    .ToList()
            )).ToList()
        )).ToList();

        var unassigned = players
            .Where(ep => ep.SquadId is null && ep.PlatoonId is null)
            .Select(ep => new PlayerDto(ep.Id, ep.Name, EffectiveCallsign(ep), ep.TeamAffiliation, ep.Role))
            .ToList();

        return new RosterHierarchyDto(platoonDtos, unassigned);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void AssertCommanderAccess(Faction faction)
    {
        if (faction.CommanderId != _currentUser.UserId)
            throw new ForbiddenException("User is not the commander of this event's faction");
    }
}
