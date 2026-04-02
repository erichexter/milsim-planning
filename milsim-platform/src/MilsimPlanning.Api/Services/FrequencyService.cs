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

    public FrequencyService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<FrequencyOverviewDto> GetFrequenciesAsync(Guid eventId)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var ev = await _db.Events
            .Include(e => e.Faction)
                .ThenInclude(f => f.Platoons)
                    .ThenInclude(p => p.Squads)
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found.");

        var faction = ev.Faction;
        var role = GetEventRole(eventId);
        var playerAssignment = await GetPlayerAssignment(eventId);

        return BuildFrequencyOverview(faction, role, playerAssignment);
    }

    public async Task UpdateCommandFrequenciesAsync(Guid eventId, UpdateFrequencyRequest request)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);
        AssertMinimumRole(AppRoles.FactionCommander);

        var ev = await _db.Events
            .Include(e => e.Faction)
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found.");

        ev.Faction.CommandPrimaryFrequency = request.Primary;
        ev.Faction.CommandBackupFrequency = request.Backup;
        await _db.SaveChangesAsync();
    }

    public async Task UpdatePlatoonFrequenciesAsync(Guid eventId, Guid platoonId, UpdateFrequencyRequest request)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var platoon = await _db.Platoons
            .Include(p => p.Faction)
            .FirstOrDefaultAsync(p => p.Id == platoonId)
            ?? throw new KeyNotFoundException($"Platoon {platoonId} not found.");

        if (platoon.Faction.EventId != eventId)
            throw new KeyNotFoundException($"Platoon {platoonId} not found.");

        AssertPlatoonWriteAccess(eventId, platoonId);
        platoon.PrimaryFrequency = request.Primary;
        platoon.BackupFrequency = request.Backup;
        await _db.SaveChangesAsync();
    }

    public async Task UpdateSquadFrequenciesAsync(Guid eventId, Guid squadId, UpdateFrequencyRequest request)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var squad = await _db.Squads
            .Include(s => s.Platoon)
                .ThenInclude(p => p.Faction)
            .FirstOrDefaultAsync(s => s.Id == squadId)
            ?? throw new KeyNotFoundException($"Squad {squadId} not found.");

        if (squad.Platoon.Faction.EventId != eventId)
            throw new KeyNotFoundException($"Squad {squadId} not found.");

        AssertSquadWriteAccess(eventId, squadId, squad.PlatoonId);
        squad.PrimaryFrequency = request.Primary;
        squad.BackupFrequency = request.Backup;
        await _db.SaveChangesAsync();
    }

    private FrequencyOverviewDto BuildFrequencyOverview(
        Faction faction, string role, PlayerAssignment assignment)
    {
        var roleLevel = AppRoles.Hierarchy.GetValueOrDefault(role, 0);

        // Command frequencies: platoon_leader and above
        CommandFrequencyDto? command = null;
        if (roleLevel >= AppRoles.Hierarchy[AppRoles.PlatoonLeader])
        {
            command = new CommandFrequencyDto(
                faction.CommandPrimaryFrequency,
                faction.CommandBackupFrequency);
        }

        // Platoon frequencies
        var platoons = new List<PlatoonFrequencyDto>();
        if (roleLevel >= AppRoles.Hierarchy[AppRoles.FactionCommander])
        {
            platoons = faction.Platoons.Select(p => new PlatoonFrequencyDto(
                p.Id, p.Name, p.PrimaryFrequency, p.BackupFrequency)).ToList();
        }
        else if (roleLevel >= AppRoles.Hierarchy[AppRoles.SquadLeader] && assignment.PlatoonId.HasValue)
        {
            var platoon = faction.Platoons.FirstOrDefault(p => p.Id == assignment.PlatoonId.Value);
            if (platoon != null)
            {
                platoons.Add(new PlatoonFrequencyDto(
                    platoon.Id, platoon.Name, platoon.PrimaryFrequency, platoon.BackupFrequency));
            }
        }

        // Squad frequencies
        var squads = new List<SquadFrequencyDto>();
        if (roleLevel >= AppRoles.Hierarchy[AppRoles.FactionCommander])
        {
            squads = faction.Platoons
                .SelectMany(p => p.Squads)
                .Select(s => new SquadFrequencyDto(
                    s.Id, s.Name, s.PlatoonId, s.PrimaryFrequency, s.BackupFrequency))
                .ToList();
        }
        else if (roleLevel >= AppRoles.Hierarchy[AppRoles.PlatoonLeader] && assignment.PlatoonId.HasValue)
        {
            var platoon = faction.Platoons.FirstOrDefault(p => p.Id == assignment.PlatoonId.Value);
            if (platoon != null)
            {
                squads = platoon.Squads.Select(s => new SquadFrequencyDto(
                    s.Id, s.Name, s.PlatoonId, s.PrimaryFrequency, s.BackupFrequency)).ToList();
            }
        }
        else if (assignment.SquadId.HasValue)
        {
            var squad = faction.Platoons
                .SelectMany(p => p.Squads)
                .FirstOrDefault(s => s.Id == assignment.SquadId.Value);
            if (squad != null)
            {
                squads.Add(new SquadFrequencyDto(
                    squad.Id, squad.Name, squad.PlatoonId, squad.PrimaryFrequency, squad.BackupFrequency));
            }
        }

        return new FrequencyOverviewDto(command, platoons, squads);
    }

    private string GetEventRole(Guid eventId)
    {
        // ICurrentUser.Role is the global role from the JWT; for event-scoped
        // visibility we use the EventMembership role.
        var membership = _db.EventMemberships
            .FirstOrDefault(m => m.UserId == _currentUser.UserId && m.EventId == eventId);
        return membership?.Role ?? _currentUser.Role;
    }

    private async Task<PlayerAssignment> GetPlayerAssignment(Guid eventId)
    {
        var player = await _db.EventPlayers
            .FirstOrDefaultAsync(ep => ep.UserId == _currentUser.UserId && ep.EventId == eventId);

        if (player == null)
            return new PlayerAssignment(null, null);

        // If player has a squad, derive platoon from squad
        if (player.SquadId.HasValue)
        {
            var squad = await _db.Squads.FindAsync(player.SquadId.Value);
            return new PlayerAssignment(squad?.PlatoonId, player.SquadId);
        }

        return new PlayerAssignment(player.PlatoonId, null);
    }

    private void AssertMinimumRole(string minimumRole)
    {
        var userLevel = AppRoles.Hierarchy.GetValueOrDefault(_currentUser.Role, 0);
        var requiredLevel = AppRoles.Hierarchy.GetValueOrDefault(minimumRole, int.MaxValue);
        if (userLevel < requiredLevel)
            throw new ForbiddenException("Insufficient role to access this resource.");
    }

    private void AssertPlatoonWriteAccess(Guid eventId, Guid platoonId)
    {
        var role = _currentUser.Role;
        var roleLevel = AppRoles.Hierarchy.GetValueOrDefault(role, 0);

        if (roleLevel >= AppRoles.Hierarchy[AppRoles.FactionCommander])
            return;

        if (role == AppRoles.PlatoonLeader)
        {
            var player = _db.EventPlayers
                .Include(ep => ep.Squad)
                .FirstOrDefault(ep => ep.UserId == _currentUser.UserId && ep.EventId == eventId);

            if (player != null)
            {
                // Check direct platoon assignment or squad's platoon
                var assignedPlatoonId = player.PlatoonId ?? player.Squad?.PlatoonId;
                if (assignedPlatoonId == platoonId)
                    return;
            }
        }

        throw new ForbiddenException("Insufficient role to access this resource.");
    }

    private void AssertSquadWriteAccess(Guid eventId, Guid squadId, Guid squadPlatoonId)
    {
        var role = _currentUser.Role;
        var roleLevel = AppRoles.Hierarchy.GetValueOrDefault(role, 0);

        if (roleLevel >= AppRoles.Hierarchy[AppRoles.FactionCommander])
            return;

        var player = _db.EventPlayers
            .Include(ep => ep.Squad)
            .FirstOrDefault(ep => ep.UserId == _currentUser.UserId && ep.EventId == eventId);

        if (player == null)
            throw new ForbiddenException("Insufficient role to access this resource.");

        if (role == AppRoles.PlatoonLeader)
        {
            var assignedPlatoonId = player.PlatoonId ?? player.Squad?.PlatoonId;
            if (assignedPlatoonId == squadPlatoonId)
                return;
        }

        if (role == AppRoles.SquadLeader)
        {
            if (player.SquadId == squadId)
                return;
        }

        throw new ForbiddenException("Insufficient role to access this resource.");
    }

    private record PlayerAssignment(Guid? PlatoonId, Guid? SquadId);
}
