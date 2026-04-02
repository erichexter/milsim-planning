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

    // ── GET /api/events/{eventId:guid}/frequencies ────────────────────────────

    public async Task<FrequenciesDto> GetFrequenciesAsync(Guid eventId)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        // Load the caller's EventPlayer (null acceptable for system_admin)
        var eventPlayer = await _db.EventPlayers
            .FirstOrDefaultAsync(ep => ep.UserId == _currentUser.UserId && ep.EventId == eventId);

        var role = _currentUser.Role;

        if (role == AppRoles.Player)
        {
            if (eventPlayer?.SquadId is null)
                return new FrequenciesDto();

            var squad = await _db.Squads.FindAsync(eventPlayer.SquadId.Value)
                ?? throw new KeyNotFoundException($"Squad {eventPlayer.SquadId} not found.");

            return new FrequenciesDto
            {
                Squad = new FrequencyLevelDto
                {
                    Id = squad.Id,
                    Name = squad.Name,
                    Primary = squad.SquadPrimaryFrequency,
                    Backup = squad.SquadBackupFrequency
                }
            };
        }

        if (role == AppRoles.SquadLeader)
        {
            if (eventPlayer?.SquadId is null)
                return new FrequenciesDto();

            var squad = await _db.Squads
                .Include(s => s.Platoon)
                .FirstOrDefaultAsync(s => s.Id == eventPlayer.SquadId.Value)
                ?? throw new KeyNotFoundException($"Squad {eventPlayer.SquadId} not found.");

            return new FrequenciesDto
            {
                Squad = new FrequencyLevelDto
                {
                    Id = squad.Id,
                    Name = squad.Name,
                    Primary = squad.SquadPrimaryFrequency,
                    Backup = squad.SquadBackupFrequency
                },
                Platoon = new FrequencyLevelDto
                {
                    Id = squad.Platoon.Id,
                    Name = squad.Platoon.Name,
                    Primary = squad.Platoon.PlatoonPrimaryFrequency,
                    Backup = squad.Platoon.PlatoonBackupFrequency
                }
            };
        }

        if (role == AppRoles.PlatoonLeader)
        {
            var platoonId = eventPlayer?.PlatoonId
                ?? (eventPlayer?.SquadId is not null
                    ? await _db.Squads
                        .Where(s => s.Id == eventPlayer.SquadId.Value)
                        .Select(s => (Guid?)s.PlatoonId)
                        .FirstOrDefaultAsync()
                    : null);

            if (platoonId is null)
                return new FrequenciesDto();

            var platoon = await _db.Platoons
                .Include(p => p.Faction)
                .FirstOrDefaultAsync(p => p.Id == platoonId.Value)
                ?? throw new KeyNotFoundException($"Platoon {platoonId} not found.");

            return new FrequenciesDto
            {
                Platoon = new FrequencyLevelDto
                {
                    Id = platoon.Id,
                    Name = platoon.Name,
                    Primary = platoon.PlatoonPrimaryFrequency,
                    Backup = platoon.PlatoonBackupFrequency
                },
                Command = new FrequencyLevelDto
                {
                    Id = platoon.Faction.Id,
                    Name = platoon.Faction.Name,
                    Primary = platoon.Faction.CommandPrimaryFrequency,
                    Backup = platoon.Faction.CommandBackupFrequency
                }
            };
        }

        // faction_commander or system_admin
        var faction = await _db.Factions
            .Include(f => f.Platoons.OrderBy(p => p.Order))
                .ThenInclude(p => p.Squads.OrderBy(s => s.Order))
            .FirstOrDefaultAsync(f => f.EventId == eventId)
            ?? throw new KeyNotFoundException($"Faction for event {eventId} not found.");

        return new FrequenciesDto
        {
            Command = new FrequencyLevelDto
            {
                Id = faction.Id,
                Name = faction.Name,
                Primary = faction.CommandPrimaryFrequency,
                Backup = faction.CommandBackupFrequency
            },
            AllPlatoons = faction.Platoons.Select(p => new FrequencyLevelDto
            {
                Id = p.Id,
                Name = p.Name,
                Primary = p.PlatoonPrimaryFrequency,
                Backup = p.PlatoonBackupFrequency
            }).ToList(),
            AllSquads = faction.Platoons
                .SelectMany(p => p.Squads)
                .Select(s => new FrequencyLevelDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    Primary = s.SquadPrimaryFrequency,
                    Backup = s.SquadBackupFrequency
                }).ToList()
        };
    }

    // ── PATCH /api/squads/{squadId:guid}/frequencies ──────────────────────────

    public async Task UpdateSquadFrequenciesAsync(Guid squadId, UpdateFrequencyRequest req)
    {
        var squad = await _db.Squads
            .Include(s => s.Platoon)
                .ThenInclude(p => p.Faction)
            .FirstOrDefaultAsync(s => s.Id == squadId)
            ?? throw new KeyNotFoundException($"Squad {squadId} not found.");

        var eventId = squad.Platoon.Faction.EventId;
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var role = _currentUser.Role;

        if (role == AppRoles.SystemAdmin || role == AppRoles.FactionCommander)
        {
            // allowed
        }
        else if (role == AppRoles.PlatoonLeader)
        {
            var callerPlatoonId = await GetCallerPlatoonIdAsync(eventId);
            if (squad.PlatoonId != callerPlatoonId)
                throw new ForbiddenException("Platoon leader cannot update a squad outside their platoon.");
        }
        else if (role == AppRoles.SquadLeader)
        {
            var eventPlayer = await _db.EventPlayers
                .FirstOrDefaultAsync(ep => ep.UserId == _currentUser.UserId && ep.EventId == eventId);
            if (eventPlayer?.SquadId != squadId)
                throw new ForbiddenException("Squad leader can only update their own squad.");
        }
        else
        {
            throw new ForbiddenException("Insufficient role to update squad frequencies.");
        }

        squad.SquadPrimaryFrequency = req.Primary;
        squad.SquadBackupFrequency = req.Backup;
        await _db.SaveChangesAsync();
    }

    // ── PATCH /api/platoons/{platoonId:guid}/frequencies ──────────────────────

    public async Task UpdatePlatoonFrequenciesAsync(Guid platoonId, UpdateFrequencyRequest req)
    {
        var platoon = await _db.Platoons
            .Include(p => p.Faction)
            .FirstOrDefaultAsync(p => p.Id == platoonId)
            ?? throw new KeyNotFoundException($"Platoon {platoonId} not found.");

        var eventId = platoon.Faction.EventId;
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var role = _currentUser.Role;

        if (role == AppRoles.SystemAdmin || role == AppRoles.FactionCommander)
        {
            // allowed
        }
        else if (role == AppRoles.PlatoonLeader)
        {
            var callerPlatoonId = await GetCallerPlatoonIdAsync(eventId);
            if (platoonId != callerPlatoonId)
                throw new ForbiddenException("Platoon leader cannot update a different platoon's frequencies.");
        }
        else
        {
            throw new ForbiddenException("Insufficient role to update platoon frequencies.");
        }

        platoon.PlatoonPrimaryFrequency = req.Primary;
        platoon.PlatoonBackupFrequency = req.Backup;
        await _db.SaveChangesAsync();
    }

    // ── PATCH /api/factions/{factionId:guid}/frequencies ──────────────────────

    public async Task UpdateFactionFrequenciesAsync(Guid factionId, UpdateFrequencyRequest req)
    {
        var faction = await _db.Factions
            .FirstOrDefaultAsync(f => f.Id == factionId)
            ?? throw new KeyNotFoundException($"Faction {factionId} not found.");

        ScopeGuard.AssertEventAccess(_currentUser, faction.EventId);

        var role = _currentUser.Role;

        if (role == AppRoles.SystemAdmin)
        {
            // allowed
        }
        else if (role == AppRoles.FactionCommander)
        {
            if (faction.CommanderId != _currentUser.UserId)
                throw new ForbiddenException("Faction commander can only update their own faction's frequencies.");
        }
        else
        {
            throw new ForbiddenException("Insufficient role to update faction frequencies.");
        }

        faction.CommandPrimaryFrequency = req.Primary;
        faction.CommandBackupFrequency = req.Backup;
        await _db.SaveChangesAsync();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<Guid?> GetCallerPlatoonIdAsync(Guid eventId)
    {
        var eventPlayer = await _db.EventPlayers
            .FirstOrDefaultAsync(ep => ep.UserId == _currentUser.UserId && ep.EventId == eventId);

        if (eventPlayer is null) return null;

        if (eventPlayer.PlatoonId.HasValue)
            return eventPlayer.PlatoonId.Value;

        if (eventPlayer.SquadId.HasValue)
            return await _db.Squads
                .Where(s => s.Id == eventPlayer.SquadId.Value)
                .Select(s => (Guid?)s.PlatoonId)
                .FirstOrDefaultAsync();

        return null;
    }
}
