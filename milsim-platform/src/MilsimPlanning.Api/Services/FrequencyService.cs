using Microsoft.EntityFrameworkCore;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Data;
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

    // ── GET /api/events/{eventId}/frequencies ─────────────────────────────────

    public async Task<EventFrequenciesDto> GetEventFrequenciesAsync(Guid eventId)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var role = _currentUser.Role;

        if (role == AppRoles.FactionCommander || role == AppRoles.SystemAdmin)
        {
            var faction = await _db.Factions
                .FirstOrDefaultAsync(f => f.EventId == eventId)
                ?? throw new KeyNotFoundException($"Faction for event {eventId} not found");

            return new EventFrequenciesDto
            {
                Command = ToDto(faction.Id, faction.Name, faction.CommandPrimaryFrequency, faction.CommandBackupFrequency)
            };
        }

        // For player / squad_leader / platoon_leader: lookup their EventPlayer row
        var eventPlayer = await _db.EventPlayers
            .Include(ep => ep.Squad)
                .ThenInclude(s => s!.Platoon)
                    .ThenInclude(p => p.Faction)
            .Include(ep => ep.Platoon)
                .ThenInclude(p => p!.Faction)
            .FirstOrDefaultAsync(ep => ep.EventId == eventId && ep.UserId == _currentUser.UserId);

        return role switch
        {
            AppRoles.PlatoonLeader => BuildPlatoonLeaderResponse(eventPlayer),
            AppRoles.SquadLeader   => BuildSquadLeaderResponse(eventPlayer),
            _                      => BuildPlayerResponse(eventPlayer)
        };
    }

    private static EventFrequenciesDto BuildPlayerResponse(Data.Entities.EventPlayer? ep)
    {
        if (ep?.Squad is null)
            return new EventFrequenciesDto();

        return new EventFrequenciesDto
        {
            Squad = ToDto(ep.Squad.Id, ep.Squad.Name, ep.Squad.PrimaryFrequency, ep.Squad.BackupFrequency)
        };
    }

    private static EventFrequenciesDto BuildSquadLeaderResponse(Data.Entities.EventPlayer? ep)
    {
        var dto = new EventFrequenciesDto();

        if (ep?.Squad is not null)
            dto.Squad = ToDto(ep.Squad.Id, ep.Squad.Name, ep.Squad.PrimaryFrequency, ep.Squad.BackupFrequency);

        var platoon = ep?.Platoon ?? ep?.Squad?.Platoon;
        if (platoon is not null)
            dto.Platoon = ToDto(platoon.Id, platoon.Name, platoon.PrimaryFrequency, platoon.BackupFrequency);

        return dto;
    }

    private static EventFrequenciesDto BuildPlatoonLeaderResponse(Data.Entities.EventPlayer? ep)
    {
        var dto = new EventFrequenciesDto();

        var platoon = ep?.Platoon ?? ep?.Squad?.Platoon;
        if (platoon is not null)
        {
            dto.Platoon = ToDto(platoon.Id, platoon.Name, platoon.PrimaryFrequency, platoon.BackupFrequency);

            var faction = platoon.Faction;
            if (faction is not null)
                dto.Command = ToDto(faction.Id, faction.Name, faction.CommandPrimaryFrequency, faction.CommandBackupFrequency);
        }

        return dto;
    }

    // ── PUT /api/squads/{squadId}/frequencies ─────────────────────────────────

    public async Task<FrequencyLevelDto> UpdateSquadFrequenciesAsync(Guid squadId, UpdateFrequencyRequest request)
    {
        var squad = await _db.Squads
            .Include(s => s.Platoon)
                .ThenInclude(p => p.Faction)
            .FirstOrDefaultAsync(s => s.Id == squadId)
            ?? throw new KeyNotFoundException($"Squad {squadId} not found");

        var eventId = squad.Platoon.Faction.EventId;
        ScopeGuard.AssertEventAccess(_currentUser, eventId);
        await AssertSquadWriteAccessAsync(squad, eventId);

        squad.PrimaryFrequency = request.Primary;
        squad.BackupFrequency = request.Backup;
        await _db.SaveChangesAsync();

        return ToDto(squad.Id, squad.Name, squad.PrimaryFrequency, squad.BackupFrequency);
    }

    // ── PUT /api/platoons/{platoonId}/frequencies ─────────────────────────────

    public async Task<FrequencyLevelDto> UpdatePlatoonFrequenciesAsync(Guid platoonId, UpdateFrequencyRequest request)
    {
        var platoon = await _db.Platoons
            .Include(p => p.Faction)
            .FirstOrDefaultAsync(p => p.Id == platoonId)
            ?? throw new KeyNotFoundException($"Platoon {platoonId} not found");

        ScopeGuard.AssertEventAccess(_currentUser, platoon.Faction.EventId);
        await AssertPlatoonWriteAccessAsync(platoon);

        platoon.PrimaryFrequency = request.Primary;
        platoon.BackupFrequency = request.Backup;
        await _db.SaveChangesAsync();

        return ToDto(platoon.Id, platoon.Name, platoon.PrimaryFrequency, platoon.BackupFrequency);
    }

    // ── PUT /api/factions/{factionId}/frequencies ─────────────────────────────

    public async Task<FrequencyLevelDto> UpdateFactionFrequenciesAsync(Guid factionId, UpdateFrequencyRequest request)
    {
        var faction = await _db.Factions
            .FirstOrDefaultAsync(f => f.Id == factionId)
            ?? throw new KeyNotFoundException($"Faction {factionId} not found");

        ScopeGuard.AssertEventAccess(_currentUser, faction.EventId);
        AssertFactionWriteAccess(faction);

        faction.CommandPrimaryFrequency = request.Primary;
        faction.CommandBackupFrequency = request.Backup;
        await _db.SaveChangesAsync();

        return ToDto(faction.Id, faction.Name, faction.CommandPrimaryFrequency, faction.CommandBackupFrequency);
    }

    // ── Write Access Guards ───────────────────────────────────────────────────

    private async Task AssertSquadWriteAccessAsync(Data.Entities.Squad squad, Guid eventId)
    {
        var role = _currentUser.Role;

        if (role == AppRoles.SystemAdmin) return;

        if (role == AppRoles.FactionCommander)
        {
            if (squad.Platoon.Faction.CommanderId != _currentUser.UserId)
                throw new ForbiddenException("Insufficient role to access this resource.");
            return;
        }

        if (role == AppRoles.PlatoonLeader)
        {
            var ep = await _db.EventPlayers
                .FirstOrDefaultAsync(e => e.EventId == eventId && e.UserId == _currentUser.UserId);
            if (ep?.PlatoonId != squad.PlatoonId)
                throw new ForbiddenException("Insufficient role to access this resource.");
            return;
        }

        if (role == AppRoles.SquadLeader)
        {
            var ep = await _db.EventPlayers
                .FirstOrDefaultAsync(e => e.EventId == eventId && e.UserId == _currentUser.UserId);
            if (ep?.SquadId != squad.Id)
                throw new ForbiddenException("Insufficient role to access this resource.");
            return;
        }

        // player or unknown role — always forbidden
        throw new ForbiddenException("Insufficient role to access this resource.");
    }

    private async Task AssertPlatoonWriteAccessAsync(Data.Entities.Platoon platoon)
    {
        var role = _currentUser.Role;

        if (role == AppRoles.SystemAdmin) return;

        if (role == AppRoles.FactionCommander)
        {
            if (platoon.Faction.CommanderId != _currentUser.UserId)
                throw new ForbiddenException("Insufficient role to access this resource.");
            return;
        }

        if (role == AppRoles.PlatoonLeader)
        {
            var ep = await _db.EventPlayers
                .FirstOrDefaultAsync(e => e.EventId == platoon.Faction.EventId && e.UserId == _currentUser.UserId);
            if (ep?.PlatoonId != platoon.Id)
                throw new ForbiddenException("Insufficient role to access this resource.");
            return;
        }

        // squad_leader, player, or unknown role — always forbidden
        throw new ForbiddenException("Insufficient role to access this resource.");
    }

    private void AssertFactionWriteAccess(Data.Entities.Faction faction)
    {
        var role = _currentUser.Role;

        if (role == AppRoles.SystemAdmin) return;

        if (role == AppRoles.FactionCommander)
        {
            if (faction.CommanderId != _currentUser.UserId)
                throw new ForbiddenException("Insufficient role to access this resource.");
            return;
        }

        throw new ForbiddenException("Insufficient role to access this resource.");
    }

    // ── Mapping helpers ───────────────────────────────────────────────────────

    private static FrequencyLevelDto ToDto(Guid id, string name, string? primary, string? backup)
        => new() { Id = id, Name = name, Primary = primary, Backup = backup };
}
