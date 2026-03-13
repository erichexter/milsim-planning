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

    public async Task<Platoon> CreatePlatoonAsync(Guid eventId, string name)
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
            Order = maxOrder + 1
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

        var platoonDtos = platoons.Select(p => new PlatoonDto(
            p.Id,
            p.Name,
            p.Squads.OrderBy(s => s.Order).Select(s => new SquadDto(
                s.Id,
                s.Name,
                players
                    .Where(ep => ep.SquadId == s.Id)
                    .Select(ep => new PlayerDto(ep.Id, ep.Name, ep.Callsign, ep.TeamAffiliation))
                    .ToList()
            )).ToList()
        )).ToList();

        var unassigned = players
            .Where(ep => ep.SquadId is null)
            .Select(ep => new PlayerDto(ep.Id, ep.Name, ep.Callsign, ep.TeamAffiliation))
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
