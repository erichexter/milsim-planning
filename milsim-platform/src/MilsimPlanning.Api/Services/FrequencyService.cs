using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Domain;
using MilsimPlanning.Api.Models.Frequencies;
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

    // ── FREQ-01: Read Frequencies (role-filtered) ───────────────────────────

    public async Task<FrequencyReadDto> GetFrequenciesAsync(Guid eventId)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var eventPlayer = await _db.EventPlayers
            .FirstOrDefaultAsync(ep => ep.EventId == eventId && ep.UserId == _currentUser.UserId)
            ?? throw new KeyNotFoundException($"EventPlayer for current user not found in event {eventId}.");

        var membership = await _db.EventMemberships
            .FirstOrDefaultAsync(m => m.EventId == eventId && m.UserId == _currentUser.UserId)
            ?? throw new ForbiddenException("User does not have a membership in this event.");

        var role = membership.Role;

        return role switch
        {
            AppRoles.Player => await BuildPlayerView(eventPlayer),
            AppRoles.SquadLeader => await BuildSquadLeaderView(eventPlayer),
            AppRoles.PlatoonLeader => await BuildPlatoonLeaderView(eventPlayer),
            AppRoles.FactionCommander or AppRoles.SystemAdmin => await BuildCommanderView(eventId),
            _ => await BuildPlayerView(eventPlayer)
        };
    }

    // ── FREQ-02: Update Squad Frequencies ───────────────────────────────────

    public async Task UpdateSquadFrequenciesAsync(Guid squadId, UpdateFrequencyRequest request)
    {
        var squad = await _db.Squads
            .Include(s => s.Platoon)
                .ThenInclude(p => p.Faction)
            .FirstOrDefaultAsync(s => s.Id == squadId)
            ?? throw new KeyNotFoundException($"Squad {squadId} not found.");

        ScopeGuard.AssertEventAccess(_currentUser, squad.Platoon.Faction.EventId);

        var eventId = squad.Platoon.Faction.EventId;
        var membership = await _db.EventMemberships
            .FirstOrDefaultAsync(m => m.EventId == eventId && m.UserId == _currentUser.UserId)
            ?? throw new ForbiddenException("User does not have a membership in this event.");

        var eventPlayer = await _db.EventPlayers
            .FirstOrDefaultAsync(ep => ep.EventId == eventId && ep.UserId == _currentUser.UserId);

        var role = membership.Role;

        switch (role)
        {
            case AppRoles.FactionCommander or AppRoles.SystemAdmin:
                break; // always pass
            case AppRoles.PlatoonLeader:
                var userPlatoonId = eventPlayer?.PlatoonId ?? eventPlayer?.Squad?.PlatoonId;
                if (userPlatoonId == null)
                {
                    // Load via squad
                    if (eventPlayer?.SquadId != null)
                    {
                        var playerSquad = await _db.Squads.FindAsync(eventPlayer.SquadId);
                        userPlatoonId = playerSquad?.PlatoonId;
                    }
                }
                if (squad.PlatoonId != userPlatoonId)
                    throw new ForbiddenException("Insufficient role to access this resource.");
                break;
            case AppRoles.SquadLeader:
                if (eventPlayer?.SquadId != squadId)
                    throw new ForbiddenException("Insufficient role to access this resource.");
                break;
            default:
                throw new ForbiddenException("Insufficient role to access this resource.");
        }

        squad.PrimaryFrequency = request.Primary;
        squad.BackupFrequency = request.Backup;
        await _db.SaveChangesAsync();
    }

    // ── FREQ-03: Update Platoon Frequencies ─────────────────────────────────

    public async Task UpdatePlatoonFrequenciesAsync(Guid platoonId, UpdateFrequencyRequest request)
    {
        var platoon = await _db.Platoons
            .Include(p => p.Faction)
            .FirstOrDefaultAsync(p => p.Id == platoonId)
            ?? throw new KeyNotFoundException($"Platoon {platoonId} not found.");

        ScopeGuard.AssertEventAccess(_currentUser, platoon.Faction.EventId);

        var eventId = platoon.Faction.EventId;
        var membership = await _db.EventMemberships
            .FirstOrDefaultAsync(m => m.EventId == eventId && m.UserId == _currentUser.UserId)
            ?? throw new ForbiddenException("User does not have a membership in this event.");

        var eventPlayer = await _db.EventPlayers
            .FirstOrDefaultAsync(ep => ep.EventId == eventId && ep.UserId == _currentUser.UserId);

        var role = membership.Role;

        switch (role)
        {
            case AppRoles.FactionCommander or AppRoles.SystemAdmin:
                break; // always pass
            case AppRoles.PlatoonLeader:
                var userPlatoonId = eventPlayer?.PlatoonId;
                if (userPlatoonId == null && eventPlayer?.SquadId != null)
                {
                    var playerSquad = await _db.Squads.FindAsync(eventPlayer.SquadId);
                    userPlatoonId = playerSquad?.PlatoonId;
                }
                if (platoonId != userPlatoonId)
                    throw new ForbiddenException("Insufficient role to access this resource.");
                break;
            default:
                throw new ForbiddenException("Insufficient role to access this resource.");
        }

        platoon.PrimaryFrequency = request.Primary;
        platoon.BackupFrequency = request.Backup;
        await _db.SaveChangesAsync();
    }

    // ── FREQ-04: Update Command (Faction) Frequencies ───────────────────────

    public async Task UpdateCommandFrequenciesAsync(Guid factionId, UpdateFrequencyRequest request)
    {
        var faction = await _db.Factions
            .FirstOrDefaultAsync(f => f.Id == factionId)
            ?? throw new KeyNotFoundException($"Faction {factionId} not found.");

        ScopeGuard.AssertEventAccess(_currentUser, faction.EventId);

        faction.CommandPrimaryFrequency = request.Primary;
        faction.CommandBackupFrequency = request.Backup;
        await _db.SaveChangesAsync();
    }

    // ── Private view builders ───────────────────────────────────────────────

    private async Task<FrequencyReadDto> BuildPlayerView(EventPlayer eventPlayer)
    {
        if (eventPlayer.SquadId == null)
            return new FrequencyReadDto();

        var squad = await _db.Squads.FindAsync(eventPlayer.SquadId.Value);
        if (squad == null) return new FrequencyReadDto();

        return new FrequencyReadDto
        {
            Squad = new SquadFrequencyDto
            {
                SquadId = squad.Id,
                SquadName = squad.Name,
                Primary = squad.PrimaryFrequency,
                Backup = squad.BackupFrequency
            }
        };
    }

    private async Task<FrequencyReadDto> BuildSquadLeaderView(EventPlayer eventPlayer)
    {
        var result = await BuildPlayerView(eventPlayer);

        // Also get platoon
        Guid? platoonId = eventPlayer.PlatoonId;
        if (platoonId == null && eventPlayer.SquadId != null)
        {
            var squad = await _db.Squads.FindAsync(eventPlayer.SquadId.Value);
            platoonId = squad?.PlatoonId;
        }

        if (platoonId != null)
        {
            var platoon = await _db.Platoons.FindAsync(platoonId.Value);
            if (platoon != null)
            {
                result.Platoon = new PlatoonFrequencyDto
                {
                    PlatoonId = platoon.Id,
                    PlatoonName = platoon.Name,
                    Primary = platoon.PrimaryFrequency,
                    Backup = platoon.BackupFrequency
                };
            }
        }

        return result;
    }

    private async Task<FrequencyReadDto> BuildPlatoonLeaderView(EventPlayer eventPlayer)
    {
        Guid? platoonId = eventPlayer.PlatoonId;
        if (platoonId == null && eventPlayer.SquadId != null)
        {
            var squad = await _db.Squads.FindAsync(eventPlayer.SquadId.Value);
            platoonId = squad?.PlatoonId;
        }

        var result = new FrequencyReadDto();

        if (platoonId != null)
        {
            var platoon = await _db.Platoons
                .Include(p => p.Faction)
                .FirstOrDefaultAsync(p => p.Id == platoonId.Value);

            if (platoon != null)
            {
                result.Platoon = new PlatoonFrequencyDto
                {
                    PlatoonId = platoon.Id,
                    PlatoonName = platoon.Name,
                    Primary = platoon.PrimaryFrequency,
                    Backup = platoon.BackupFrequency
                };

                result.Command = new CommandFrequencyDto
                {
                    FactionId = platoon.Faction.Id,
                    FactionName = platoon.Faction.Name,
                    Primary = platoon.Faction.CommandPrimaryFrequency,
                    Backup = platoon.Faction.CommandBackupFrequency
                };
            }
        }

        return result;
    }

    private async Task<FrequencyReadDto> BuildCommanderView(Guid eventId)
    {
        var faction = await _db.Factions
            .Include(f => f.Platoons)
                .ThenInclude(p => p.Squads)
            .FirstOrDefaultAsync(f => f.EventId == eventId);

        if (faction == null) return new FrequencyReadDto();

        var commandDto = new CommandFrequencyDto
        {
            FactionId = faction.Id,
            FactionName = faction.Name,
            Primary = faction.CommandPrimaryFrequency,
            Backup = faction.CommandBackupFrequency
        };

        return new FrequencyReadDto
        {
            Command = commandDto,
            AllFrequencies = new AllFrequenciesDto
            {
                Command = commandDto,
                Platoons = faction.Platoons.Select(p => new PlatoonFrequencyDto
                {
                    PlatoonId = p.Id,
                    PlatoonName = p.Name,
                    Primary = p.PrimaryFrequency,
                    Backup = p.BackupFrequency
                }).ToList(),
                Squads = faction.Platoons.SelectMany(p => p.Squads.Select(s => new AllFrequenciesSquadDto
                {
                    SquadId = s.Id,
                    SquadName = s.Name,
                    PlatoonName = p.Name,
                    Primary = s.PrimaryFrequency,
                    Backup = s.BackupFrequency
                })).ToList()
            }
        };
    }
}
