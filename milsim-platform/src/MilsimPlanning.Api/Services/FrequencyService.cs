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

    public async Task<EventFrequenciesDto> GetFrequenciesAsync(Guid eventId)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var faction = await _db.Factions
            .Include(f => f.Platoons)
                .ThenInclude(p => p.Squads)
            .FirstOrDefaultAsync(f => f.EventId == eventId)
            ?? throw new KeyNotFoundException($"Faction for event {eventId} not found");

        var membership = await _db.EventMemberships
            .FirstOrDefaultAsync(m => m.UserId == _currentUser.UserId && m.EventId == eventId)
            ?? throw new ForbiddenException("Insufficient role to access this resource");

        var eventPlayer = await _db.EventPlayers
            .FirstOrDefaultAsync(ep => ep.EventId == eventId && ep.UserId == _currentUser.UserId);

        var role = membership.Role;

        return role switch
        {
            AppRoles.FactionCommander or AppRoles.SystemAdmin => BuildFullView(faction),
            AppRoles.PlatoonLeader => BuildPlatoonLeaderView(faction, eventPlayer),
            AppRoles.SquadLeader => BuildSquadLeaderView(faction, eventPlayer),
            _ => BuildPlayerView(faction, eventPlayer) // player
        };
    }

    public async Task UpdateSquadFrequencyAsync(Guid squadId, string? primary, string? backup)
    {
        var squad = await _db.Squads
            .Include(s => s.Platoon)
                .ThenInclude(p => p.Faction)
            .FirstOrDefaultAsync(s => s.Id == squadId)
            ?? throw new KeyNotFoundException($"Squad {squadId} not found");

        var eventId = squad.Platoon.Faction.EventId;
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var membership = await _db.EventMemberships
            .FirstOrDefaultAsync(m => m.UserId == _currentUser.UserId && m.EventId == eventId);

        var eventPlayer = await _db.EventPlayers
            .FirstOrDefaultAsync(ep => ep.EventId == eventId && ep.UserId == _currentUser.UserId);

        var role = membership?.Role ?? AppRoles.Player;

        // system_admin bypasses scope guard but may not have a membership
        if (_currentUser.Role == AppRoles.SystemAdmin)
            role = AppRoles.SystemAdmin;

        switch (role)
        {
            case AppRoles.FactionCommander or AppRoles.SystemAdmin:
                break; // can edit any squad
            case AppRoles.PlatoonLeader:
                if (eventPlayer?.PlatoonId != squad.PlatoonId)
                    throw new ForbiddenException("Insufficient role to access this resource");
                break;
            case AppRoles.SquadLeader:
                if (eventPlayer?.SquadId != squadId)
                    throw new ForbiddenException("Insufficient role to access this resource");
                break;
            default:
                throw new ForbiddenException("Insufficient role to access this resource");
        }

        squad.PrimaryFrequency = primary;
        squad.BackupFrequency = backup;
        await _db.SaveChangesAsync();
    }

    public async Task UpdatePlatoonFrequencyAsync(Guid platoonId, string? primary, string? backup)
    {
        var platoon = await _db.Platoons
            .Include(p => p.Faction)
            .FirstOrDefaultAsync(p => p.Id == platoonId)
            ?? throw new KeyNotFoundException($"Platoon {platoonId} not found");

        var eventId = platoon.Faction.EventId;
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var membership = await _db.EventMemberships
            .FirstOrDefaultAsync(m => m.UserId == _currentUser.UserId && m.EventId == eventId);

        var eventPlayer = await _db.EventPlayers
            .FirstOrDefaultAsync(ep => ep.EventId == eventId && ep.UserId == _currentUser.UserId);

        var role = membership?.Role ?? AppRoles.Player;

        if (_currentUser.Role == AppRoles.SystemAdmin)
            role = AppRoles.SystemAdmin;

        switch (role)
        {
            case AppRoles.FactionCommander or AppRoles.SystemAdmin:
                break; // can edit any platoon
            case AppRoles.PlatoonLeader:
                if (eventPlayer?.PlatoonId != platoonId)
                    throw new ForbiddenException("Insufficient role to access this resource");
                break;
            default:
                throw new ForbiddenException("Insufficient role to access this resource");
        }

        platoon.PrimaryFrequency = primary;
        platoon.BackupFrequency = backup;
        await _db.SaveChangesAsync();
    }

    public async Task UpdateCommandFrequencyAsync(Guid eventId, string? primary, string? backup)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var faction = await _db.Factions
            .FirstOrDefaultAsync(f => f.EventId == eventId)
            ?? throw new KeyNotFoundException($"Faction for event {eventId} not found");

        var membership = await _db.EventMemberships
            .FirstOrDefaultAsync(m => m.UserId == _currentUser.UserId && m.EventId == eventId);

        var role = membership?.Role ?? AppRoles.Player;

        if (_currentUser.Role == AppRoles.SystemAdmin)
            role = AppRoles.SystemAdmin;

        if (role is not (AppRoles.FactionCommander or AppRoles.SystemAdmin))
            throw new ForbiddenException("Insufficient role to access this resource");

        faction.CommandPrimaryFrequency = primary;
        faction.CommandBackupFrequency = backup;
        await _db.SaveChangesAsync();
    }

    private static EventFrequenciesDto BuildFullView(Faction faction)
    {
        return new EventFrequenciesDto
        {
            Command = new FrequencyPairDto
            {
                Primary = faction.CommandPrimaryFrequency,
                Backup = faction.CommandBackupFrequency
            },
            Platoons = faction.Platoons.Select(p => new PlatoonFrequencyDto
            {
                PlatoonId = p.Id,
                PlatoonName = p.Name,
                Primary = p.PrimaryFrequency,
                Backup = p.BackupFrequency
            }).ToList(),
            Squads = faction.Platoons.SelectMany(p => p.Squads).Select(s => new SquadFrequencyDto
            {
                SquadId = s.Id,
                SquadName = s.Name,
                PlatoonId = s.PlatoonId,
                Primary = s.PrimaryFrequency,
                Backup = s.BackupFrequency
            }).ToList()
        };
    }

    private static EventFrequenciesDto BuildPlatoonLeaderView(Faction faction, EventPlayer? eventPlayer)
    {
        var platoon = eventPlayer?.PlatoonId != null
            ? faction.Platoons.FirstOrDefault(p => p.Id == eventPlayer.PlatoonId)
            : null;

        return new EventFrequenciesDto
        {
            Command = new FrequencyPairDto
            {
                Primary = faction.CommandPrimaryFrequency,
                Backup = faction.CommandBackupFrequency
            },
            Platoons = platoon != null
                ? [new PlatoonFrequencyDto
                {
                    PlatoonId = platoon.Id,
                    PlatoonName = platoon.Name,
                    Primary = platoon.PrimaryFrequency,
                    Backup = platoon.BackupFrequency
                }]
                : [],
            Squads = []
        };
    }

    private static EventFrequenciesDto BuildSquadLeaderView(Faction faction, EventPlayer? eventPlayer)
    {
        var platoon = eventPlayer?.PlatoonId != null
            ? faction.Platoons.FirstOrDefault(p => p.Id == eventPlayer.PlatoonId)
            : null;

        var squad = eventPlayer?.SquadId != null
            ? faction.Platoons.SelectMany(p => p.Squads).FirstOrDefault(s => s.Id == eventPlayer.SquadId)
            : null;

        return new EventFrequenciesDto
        {
            Command = null,
            Platoons = platoon != null
                ? [new PlatoonFrequencyDto
                {
                    PlatoonId = platoon.Id,
                    PlatoonName = platoon.Name,
                    Primary = platoon.PrimaryFrequency,
                    Backup = platoon.BackupFrequency
                }]
                : [],
            Squads = squad != null
                ? [new SquadFrequencyDto
                {
                    SquadId = squad.Id,
                    SquadName = squad.Name,
                    PlatoonId = squad.PlatoonId,
                    Primary = squad.PrimaryFrequency,
                    Backup = squad.BackupFrequency
                }]
                : []
        };
    }

    private static EventFrequenciesDto BuildPlayerView(Faction faction, EventPlayer? eventPlayer)
    {
        var squad = eventPlayer?.SquadId != null
            ? faction.Platoons.SelectMany(p => p.Squads).FirstOrDefault(s => s.Id == eventPlayer.SquadId)
            : null;

        return new EventFrequenciesDto
        {
            Command = null,
            Platoons = [],
            Squads = squad != null
                ? [new SquadFrequencyDto
                {
                    SquadId = squad.Id,
                    SquadName = squad.Name,
                    PlatoonId = squad.PlatoonId,
                    Primary = squad.PrimaryFrequency,
                    Backup = squad.BackupFrequency
                }]
                : []
        };
    }
}
