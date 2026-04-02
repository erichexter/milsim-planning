using Microsoft.EntityFrameworkCore;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Domain;
using MilsimPlanning.Api.Models.Frequency;

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

    public async Task<FrequencyResponseDto> GetFrequenciesAsync(Guid eventId)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var faction = await _db.Factions
            .Include(f => f.Platoons)
                .ThenInclude(p => p.Squads)
            .FirstOrDefaultAsync(f => f.EventId == eventId);

        if (faction is null)
            throw new KeyNotFoundException($"Event {eventId} not found.");

        var membership = await _db.EventMemberships
            .FirstOrDefaultAsync(m => m.UserId == _currentUser.UserId && m.EventId == eventId);

        if (membership is null)
            throw new ForbiddenException($"User does not have access to event {eventId}");

        var role = membership.Role;

        // Find caller's EventPlayer to determine squad/platoon assignment
        var eventPlayer = await _db.EventPlayers
            .FirstOrDefaultAsync(ep => ep.UserId == _currentUser.UserId && ep.EventId == eventId);

        var response = new FrequencyResponseDto();

        // Command frequencies — platoon_leader, faction_commander, system_admin
        if (role is AppRoles.PlatoonLeader or AppRoles.FactionCommander or AppRoles.SystemAdmin)
        {
            response.Command = new FrequencyBandDto
            {
                Primary = faction.CommandPrimaryFrequency,
                Backup = faction.CommandBackupFrequency
            };
        }

        // Platoon frequencies
        if (role == AppRoles.SquadLeader)
        {
            // Own platoon only
            if (eventPlayer?.PlatoonId is not null)
            {
                var platoon = faction.Platoons.FirstOrDefault(p => p.Id == eventPlayer.PlatoonId);
                if (platoon is not null)
                {
                    response.Platoons = [MapPlatoonDto(platoon)];
                }
            }
        }
        else if (role == AppRoles.PlatoonLeader)
        {
            // Own platoon only
            if (eventPlayer?.PlatoonId is not null)
            {
                var platoon = faction.Platoons.FirstOrDefault(p => p.Id == eventPlayer.PlatoonId);
                if (platoon is not null)
                {
                    response.Platoons = [MapPlatoonDto(platoon)];
                }
            }
        }
        else if (role is AppRoles.FactionCommander or AppRoles.SystemAdmin)
        {
            // All platoons
            response.Platoons = faction.Platoons
                .Select(MapPlatoonDto)
                .ToList();
        }

        // Squad frequencies
        if (role is AppRoles.Player or AppRoles.SquadLeader)
        {
            // Own squad only
            if (eventPlayer?.SquadId is not null)
            {
                var squad = faction.Platoons
                    .SelectMany(p => p.Squads)
                    .FirstOrDefault(s => s.Id == eventPlayer.SquadId);
                if (squad is not null)
                {
                    response.Squads = [MapSquadDto(squad)];
                }
            }
        }
        else if (role is AppRoles.FactionCommander or AppRoles.SystemAdmin)
        {
            // All squads
            response.Squads = faction.Platoons
                .SelectMany(p => p.Squads)
                .Select(MapSquadDto)
                .ToList();
        }

        return response;
    }

    public async Task UpdateSquadFrequencyAsync(Guid squadId, string? primary, string? backup)
    {
        var squad = await _db.Squads
            .Include(s => s.Platoon)
                .ThenInclude(p => p.Faction)
            .FirstOrDefaultAsync(s => s.Id == squadId);

        if (squad is null)
            throw new KeyNotFoundException($"Squad {squadId} not found.");

        ScopeGuard.AssertEventAccess(_currentUser, squad.Platoon.Faction.EventId);
        await AssertSquadEditAccess(squad);

        squad.PrimaryFrequency = primary;
        squad.BackupFrequency = backup;
        await _db.SaveChangesAsync();
    }

    public async Task UpdatePlatoonFrequencyAsync(Guid platoonId, string? primary, string? backup)
    {
        var platoon = await _db.Platoons
            .Include(p => p.Faction)
            .FirstOrDefaultAsync(p => p.Id == platoonId);

        if (platoon is null)
            throw new KeyNotFoundException($"Platoon {platoonId} not found.");

        ScopeGuard.AssertEventAccess(_currentUser, platoon.Faction.EventId);
        await AssertPlatoonEditAccess(platoon);

        platoon.PrimaryFrequency = primary;
        platoon.BackupFrequency = backup;
        await _db.SaveChangesAsync();
    }

    public async Task UpdateCommandFrequencyAsync(Guid eventId, string? primary, string? backup)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var faction = await _db.Factions
            .FirstOrDefaultAsync(f => f.EventId == eventId);

        if (faction is null)
            throw new KeyNotFoundException($"Event {eventId} not found.");

        if (_currentUser.Role != AppRoles.SystemAdmin && faction.CommanderId != _currentUser.UserId)
            throw new ForbiddenException("Insufficient role to edit command frequencies.");

        faction.CommandPrimaryFrequency = primary;
        faction.CommandBackupFrequency = backup;
        await _db.SaveChangesAsync();
    }

    private async Task AssertSquadEditAccess(Squad squad)
    {
        var role = _currentUser.Role;

        // system_admin and faction_commander can always edit
        if (role == AppRoles.SystemAdmin) return;
        if (role == AppRoles.FactionCommander && squad.Platoon.Faction.CommanderId == _currentUser.UserId) return;

        // platoon_leader of parent platoon
        if (role == AppRoles.PlatoonLeader)
        {
            var eventPlayer = await _db.EventPlayers
                .FirstOrDefaultAsync(ep => ep.UserId == _currentUser.UserId && ep.PlatoonId == squad.PlatoonId);
            if (eventPlayer is not null) return;
        }

        // squad_leader of THIS squad
        if (role == AppRoles.SquadLeader)
        {
            var eventPlayer = await _db.EventPlayers
                .FirstOrDefaultAsync(ep => ep.UserId == _currentUser.UserId && ep.SquadId == squad.Id);
            if (eventPlayer is not null) return;
        }

        throw new ForbiddenException("Insufficient role to edit squad frequencies.");
    }

    private async Task AssertPlatoonEditAccess(Platoon platoon)
    {
        var role = _currentUser.Role;

        // system_admin and faction_commander can always edit
        if (role == AppRoles.SystemAdmin) return;
        if (role == AppRoles.FactionCommander && platoon.Faction.CommanderId == _currentUser.UserId) return;

        // platoon_leader of THIS platoon
        if (role == AppRoles.PlatoonLeader)
        {
            var eventPlayer = await _db.EventPlayers
                .FirstOrDefaultAsync(ep => ep.UserId == _currentUser.UserId && ep.PlatoonId == platoon.Id);
            if (eventPlayer is not null) return;
        }

        throw new ForbiddenException("Insufficient role to edit platoon frequencies.");
    }

    private static PlatoonFrequencyDto MapPlatoonDto(Platoon platoon) => new()
    {
        PlatoonId = platoon.Id,
        PlatoonName = platoon.Name,
        Primary = platoon.PrimaryFrequency,
        Backup = platoon.BackupFrequency
    };

    private static SquadFrequencyDto MapSquadDto(Squad squad) => new()
    {
        SquadId = squad.Id,
        SquadName = squad.Name,
        PlatoonId = squad.PlatoonId,
        Primary = squad.PrimaryFrequency,
        Backup = squad.BackupFrequency
    };
}
