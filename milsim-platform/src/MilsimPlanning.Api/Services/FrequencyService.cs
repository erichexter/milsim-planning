using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Domain;
using MilsimPlanning.Api.Models.Frequency;
using Microsoft.EntityFrameworkCore;

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

    // ── GET /api/events/{eventId}/frequencies — role-scoped overview ──────────

    public async Task<FrequencyViewDto> GetEventFrequenciesAsync(Guid eventId)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var role = _currentUser.Role;

        if (role == AppRoles.FactionCommander || role == AppRoles.SystemAdmin)
        {
            var faction = await _db.Factions
                .Include(f => f.Platoons)
                    .ThenInclude(p => p.Squads)
                .FirstOrDefaultAsync(f => f.EventId == eventId)
                ?? throw new KeyNotFoundException($"Faction for event {eventId} not found.");

            var commandDto = new FrequencyLevelDto
            {
                Id = faction.Id,
                Name = faction.Name,
                Primary = faction.CommandPrimaryFrequency,
                Backup = faction.CommandBackupFrequency
            };

            var platoonDtos = faction.Platoons
                .Select(p => new FrequencyLevelDto { Id = p.Id, Name = p.Name, Primary = p.PrimaryFrequency, Backup = p.BackupFrequency })
                .ToArray();

            var squadDtos = faction.Platoons
                .SelectMany(p => p.Squads)
                .Select(s => new FrequencyLevelDto { Id = s.Id, Name = s.Name, Primary = s.PrimaryFrequency, Backup = s.BackupFrequency })
                .ToArray();

            return new FrequencyViewDto { Command = commandDto, Platoons = platoonDtos, Squads = squadDtos };
        }

        if (role == AppRoles.PlatoonLeader)
        {
            var eventPlayer = await _db.EventPlayers
                .FirstOrDefaultAsync(ep => ep.EventId == eventId && ep.UserId == _currentUser.UserId)
                ?? throw new KeyNotFoundException($"EventPlayer for event {eventId} not found.");

            if (eventPlayer.PlatoonId is null)
                return new FrequencyViewDto { Command = null, Platoons = [], Squads = null };

            var platoon = await _db.Platoons
                .Include(p => p.Faction)
                .FirstOrDefaultAsync(p => p.Id == eventPlayer.PlatoonId)
                ?? throw new KeyNotFoundException($"Platoon {eventPlayer.PlatoonId} not found.");

            return new FrequencyViewDto
            {
                Command = new FrequencyLevelDto { Id = platoon.Faction.Id, Name = platoon.Faction.Name, Primary = platoon.Faction.CommandPrimaryFrequency, Backup = platoon.Faction.CommandBackupFrequency },
                Platoons = [new FrequencyLevelDto { Id = platoon.Id, Name = platoon.Name, Primary = platoon.PrimaryFrequency, Backup = platoon.BackupFrequency }],
                Squads = null
            };
        }

        if (role == AppRoles.SquadLeader)
        {
            var eventPlayer = await _db.EventPlayers
                .FirstOrDefaultAsync(ep => ep.EventId == eventId && ep.UserId == _currentUser.UserId)
                ?? throw new KeyNotFoundException($"EventPlayer for event {eventId} not found.");

            if (eventPlayer.SquadId is null)
                return new FrequencyViewDto { Command = null, Platoons = null, Squads = [] };

            var squad = await _db.Squads
                .Include(s => s.Platoon)
                .FirstOrDefaultAsync(s => s.Id == eventPlayer.SquadId)
                ?? throw new KeyNotFoundException($"Squad {eventPlayer.SquadId} not found.");

            return new FrequencyViewDto
            {
                Command = null,
                Platoons = [new FrequencyLevelDto { Id = squad.Platoon.Id, Name = squad.Platoon.Name, Primary = squad.Platoon.PrimaryFrequency, Backup = squad.Platoon.BackupFrequency }],
                Squads = [new FrequencyLevelDto { Id = squad.Id, Name = squad.Name, Primary = squad.PrimaryFrequency, Backup = squad.BackupFrequency }]
            };
        }

        // player: return only their squad
        {
            var eventPlayer = await _db.EventPlayers
                .FirstOrDefaultAsync(ep => ep.EventId == eventId && ep.UserId == _currentUser.UserId)
                ?? throw new KeyNotFoundException($"EventPlayer for event {eventId} not found.");

            if (eventPlayer.SquadId is null)
                return new FrequencyViewDto { Command = null, Platoons = null, Squads = [] };

            var squad = await _db.Squads
                .FirstOrDefaultAsync(s => s.Id == eventPlayer.SquadId)
                ?? throw new KeyNotFoundException($"Squad {eventPlayer.SquadId} not found.");

            return new FrequencyViewDto
            {
                Command = null,
                Platoons = null,
                Squads = [new FrequencyLevelDto { Id = squad.Id, Name = squad.Name, Primary = squad.PrimaryFrequency, Backup = squad.BackupFrequency }]
            };
        }
    }

    // ── GET /api/squads/{squadId}/frequencies ─────────────────────────────────

    public async Task<FrequencyLevelDto> GetSquadFrequenciesAsync(Guid squadId)
    {
        var squad = await _db.Squads
            .Include(s => s.Platoon)
                .ThenInclude(p => p.Faction)
            .FirstOrDefaultAsync(s => s.Id == squadId)
            ?? throw new KeyNotFoundException($"Squad {squadId} not found.");

        ScopeGuard.AssertEventAccess(_currentUser, squad.Platoon.Faction.EventId);

        return new FrequencyLevelDto { Id = squad.Id, Name = squad.Name, Primary = squad.PrimaryFrequency, Backup = squad.BackupFrequency };
    }

    // ── PUT /api/squads/{squadId}/frequencies ─────────────────────────────────

    public async Task UpdateSquadFrequenciesAsync(Guid squadId, UpdateFrequencyRequest request)
    {
        var squad = await _db.Squads
            .Include(s => s.Platoon)
                .ThenInclude(p => p.Faction)
            .FirstOrDefaultAsync(s => s.Id == squadId)
            ?? throw new KeyNotFoundException($"Squad {squadId} not found.");

        var eventId = squad.Platoon.Faction.EventId;
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var role = _currentUser.Role;
        if (role == AppRoles.SquadLeader)
        {
            var ep = await _db.EventPlayers
                .FirstOrDefaultAsync(ep => ep.EventId == eventId && ep.UserId == _currentUser.UserId);
            if (ep?.SquadId != squadId)
                throw new ForbiddenException("Insufficient role to access this resource.");
        }
        else if (role == AppRoles.PlatoonLeader)
        {
            var ep = await _db.EventPlayers
                .FirstOrDefaultAsync(ep => ep.EventId == eventId && ep.UserId == _currentUser.UserId);
            if (ep?.PlatoonId != squad.PlatoonId)
                throw new ForbiddenException("Insufficient role to access this resource.");
        }

        squad.PrimaryFrequency = request.Primary;
        squad.BackupFrequency = request.Backup;
        await _db.SaveChangesAsync();
    }

    // ── GET /api/platoons/{platoonId}/frequencies ─────────────────────────────

    public async Task<FrequencyLevelDto> GetPlatoonFrequenciesAsync(Guid platoonId)
    {
        var platoon = await _db.Platoons
            .Include(p => p.Faction)
            .FirstOrDefaultAsync(p => p.Id == platoonId)
            ?? throw new KeyNotFoundException($"Platoon {platoonId} not found.");

        ScopeGuard.AssertEventAccess(_currentUser, platoon.Faction.EventId);

        return new FrequencyLevelDto { Id = platoon.Id, Name = platoon.Name, Primary = platoon.PrimaryFrequency, Backup = platoon.BackupFrequency };
    }

    // ── PUT /api/platoons/{platoonId}/frequencies ─────────────────────────────

    public async Task UpdatePlatoonFrequenciesAsync(Guid platoonId, UpdateFrequencyRequest request)
    {
        var platoon = await _db.Platoons
            .Include(p => p.Faction)
            .FirstOrDefaultAsync(p => p.Id == platoonId)
            ?? throw new KeyNotFoundException($"Platoon {platoonId} not found.");

        var eventId = platoon.Faction.EventId;
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var role = _currentUser.Role;
        if (role == AppRoles.PlatoonLeader)
        {
            var ep = await _db.EventPlayers
                .FirstOrDefaultAsync(ep => ep.EventId == eventId && ep.UserId == _currentUser.UserId);
            if (ep?.PlatoonId != platoonId)
                throw new ForbiddenException("Insufficient role to access this resource.");
        }
        else if (role == AppRoles.SquadLeader)
        {
            throw new ForbiddenException("Insufficient role to access this resource.");
        }

        platoon.PrimaryFrequency = request.Primary;
        platoon.BackupFrequency = request.Backup;
        await _db.SaveChangesAsync();
    }

    // ── GET /api/factions/{factionId}/command-frequencies ────────────────────

    public async Task<FrequencyLevelDto> GetFactionCommandFrequenciesAsync(Guid factionId)
    {
        var faction = await _db.Factions
            .FirstOrDefaultAsync(f => f.Id == factionId)
            ?? throw new KeyNotFoundException($"Faction {factionId} not found.");

        ScopeGuard.AssertEventAccess(_currentUser, faction.EventId);

        return new FrequencyLevelDto { Id = faction.Id, Name = faction.Name, Primary = faction.CommandPrimaryFrequency, Backup = faction.CommandBackupFrequency };
    }

    // ── PUT /api/factions/{factionId}/command-frequencies ────────────────────

    public async Task UpdateFactionCommandFrequenciesAsync(Guid factionId, UpdateFrequencyRequest request)
    {
        var faction = await _db.Factions
            .FirstOrDefaultAsync(f => f.Id == factionId)
            ?? throw new KeyNotFoundException($"Faction {factionId} not found.");

        ScopeGuard.AssertEventAccess(_currentUser, faction.EventId);

        if (_currentUser.Role != AppRoles.SystemAdmin && faction.CommanderId != _currentUser.UserId)
            throw new ForbiddenException("Insufficient role to access this resource.");

        faction.CommandPrimaryFrequency = request.Primary;
        faction.CommandBackupFrequency = request.Backup;
        await _db.SaveChangesAsync();
    }
}
