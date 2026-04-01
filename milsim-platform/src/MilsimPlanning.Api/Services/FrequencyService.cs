using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;
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

    public async Task<FrequencyVisibilityDto> GetFrequenciesAsync(Guid eventId)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var eventPlayer = await _db.EventPlayers
            .Include(ep => ep.Squad)
                .ThenInclude(s => s!.Platoon)
            .Include(ep => ep.Platoon)
                .ThenInclude(p => p!.Faction)
            .FirstOrDefaultAsync(ep => ep.EventId == eventId && ep.UserId == _currentUser.UserId);

        SquadFrequencyDto? squadDto = null;
        PlatoonFrequencyDto? platoonDto = null;
        CommandFrequencyDto? commandDto = null;

        var role = _currentUser.Role;

        if (role == AppRoles.Player)
        {
            if (eventPlayer?.Squad is not null)
                squadDto = new SquadFrequencyDto(eventPlayer.Squad.Id, eventPlayer.Squad.Name, eventPlayer.Squad.PrimaryFrequency, eventPlayer.Squad.BackupFrequency);
        }
        else if (role == AppRoles.SquadLeader)
        {
            if (eventPlayer?.Squad is not null)
            {
                squadDto = new SquadFrequencyDto(eventPlayer.Squad.Id, eventPlayer.Squad.Name, eventPlayer.Squad.PrimaryFrequency, eventPlayer.Squad.BackupFrequency);
                if (eventPlayer.Squad.Platoon is not null)
                    platoonDto = new PlatoonFrequencyDto(eventPlayer.Squad.Platoon.Id, eventPlayer.Squad.Platoon.Name, eventPlayer.Squad.Platoon.PrimaryFrequency, eventPlayer.Squad.Platoon.BackupFrequency);
            }
        }
        else if (role == AppRoles.PlatoonLeader)
        {
            if (eventPlayer?.Platoon is not null)
            {
                platoonDto = new PlatoonFrequencyDto(eventPlayer.Platoon.Id, eventPlayer.Platoon.Name, eventPlayer.Platoon.PrimaryFrequency, eventPlayer.Platoon.BackupFrequency);
                if (eventPlayer.Platoon.Faction is not null)
                    commandDto = new CommandFrequencyDto(eventPlayer.Platoon.Faction.Id, eventPlayer.Platoon.Faction.CommandPrimaryFrequency, eventPlayer.Platoon.Faction.CommandBackupFrequency);
            }
        }
        else if (role == AppRoles.FactionCommander || role == AppRoles.SystemAdmin)
        {
            var faction = await _db.Factions
                .FirstOrDefaultAsync(f => f.CommanderId == _currentUser.UserId && f.EventId == eventId);
            if (faction is not null)
                commandDto = new CommandFrequencyDto(faction.Id, faction.CommandPrimaryFrequency, faction.CommandBackupFrequency);

            if (eventPlayer?.Squad is not null)
                squadDto = new SquadFrequencyDto(eventPlayer.Squad.Id, eventPlayer.Squad.Name, eventPlayer.Squad.PrimaryFrequency, eventPlayer.Squad.BackupFrequency);

            if (eventPlayer?.Platoon is not null && eventPlayer.Squad is null)
                platoonDto = new PlatoonFrequencyDto(eventPlayer.Platoon.Id, eventPlayer.Platoon.Name, eventPlayer.Platoon.PrimaryFrequency, eventPlayer.Platoon.BackupFrequency);
        }

        return new FrequencyVisibilityDto(squadDto, platoonDto, commandDto);
    }

    public async Task UpdateSquadFrequencyAsync(Guid squadId, string? primary, string? backup)
    {
        var squad = await _db.Squads
            .Include(s => s.Platoon)
                .ThenInclude(p => p.Faction)
            .FirstOrDefaultAsync(s => s.Id == squadId)
            ?? throw new KeyNotFoundException($"Squad {squadId} not found.");

        AssertCommanderAccess(squad.Platoon.Faction);

        squad.PrimaryFrequency = primary;
        squad.BackupFrequency = backup;
        await _db.SaveChangesAsync();
    }

    public async Task UpdatePlatoonFrequencyAsync(Guid platoonId, string? primary, string? backup)
    {
        var platoon = await _db.Platoons
            .Include(p => p.Faction)
            .FirstOrDefaultAsync(p => p.Id == platoonId)
            ?? throw new KeyNotFoundException($"Platoon {platoonId} not found.");

        AssertCommanderAccess(platoon.Faction);

        platoon.PrimaryFrequency = primary;
        platoon.BackupFrequency = backup;
        await _db.SaveChangesAsync();
    }

    public async Task UpdateFactionFrequencyAsync(Guid factionId, string? primary, string? backup)
    {
        var faction = await _db.Factions
            .FirstOrDefaultAsync(f => f.Id == factionId)
            ?? throw new KeyNotFoundException($"Faction {factionId} not found.");

        AssertCommanderAccess(faction);

        faction.CommandPrimaryFrequency = primary;
        faction.CommandBackupFrequency = backup;
        await _db.SaveChangesAsync();
    }

    private void AssertCommanderAccess(Faction faction)
    {
        if (faction.CommanderId != _currentUser.UserId)
            throw new ForbiddenException("User is not the commander of this faction");
    }
}
