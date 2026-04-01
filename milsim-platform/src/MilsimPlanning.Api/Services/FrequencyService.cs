using Microsoft.EntityFrameworkCore;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Data;
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

    // ── GET /api/events/{eventId}/frequencies ─────────────────────────────────

    public async Task<EventFrequenciesDto> GetEventFrequenciesAsync(Guid eventId)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var role = _currentUser.Role;

        // Load the current user's event player record for assignment context
        var eventPlayer = await _db.EventPlayers
            .Include(ep => ep.Squad)
            .Include(ep => ep.Platoon)
                .ThenInclude(p => p!.Faction)
            .FirstOrDefaultAsync(ep => ep.EventId == eventId && ep.UserId == _currentUser.UserId);

        FrequencyLevelDto? squadLevel = null;
        FrequencyLevelDto? platoonLevel = null;
        FrequencyLevelDto? commandLevel = null;

        if (role == AppRoles.Player)
        {
            // Squad member: sees squad level only
            if (eventPlayer?.Squad != null)
                squadLevel = new FrequencyLevelDto(eventPlayer.Squad.PrimaryFrequency, eventPlayer.Squad.BackupFrequency);
        }
        else if (role == AppRoles.SquadLeader)
        {
            // Squad leader: sees squad + platoon
            if (eventPlayer?.Squad != null)
                squadLevel = new FrequencyLevelDto(eventPlayer.Squad.PrimaryFrequency, eventPlayer.Squad.BackupFrequency);

            if (eventPlayer?.Squad?.Platoon != null)
                platoonLevel = new FrequencyLevelDto(eventPlayer.Squad.Platoon.PrimaryFrequency, eventPlayer.Squad.Platoon.BackupFrequency);
            else if (eventPlayer?.Platoon != null)
                platoonLevel = new FrequencyLevelDto(eventPlayer.Platoon.PrimaryFrequency, eventPlayer.Platoon.BackupFrequency);
        }
        else if (role == AppRoles.PlatoonLeader)
        {
            // Platoon leader: sees platoon + command (faction)
            var platoon = eventPlayer?.Platoon
                ?? (eventPlayer?.Squad != null
                    ? await _db.Platoons.Include(p => p.Faction).FirstOrDefaultAsync(p => p.Id == eventPlayer.Squad.PlatoonId)
                    : null);

            if (platoon != null)
            {
                platoonLevel = new FrequencyLevelDto(platoon.PrimaryFrequency, platoon.BackupFrequency);

                var faction = platoon.Faction
                    ?? await _db.Factions.FirstOrDefaultAsync(f => f.Id == platoon.FactionId);

                if (faction != null)
                    commandLevel = new FrequencyLevelDto(faction.PrimaryFrequency, faction.BackupFrequency);
            }
        }
        else if (role == AppRoles.FactionCommander || role == AppRoles.SystemAdmin)
        {
            // Faction commander: all available levels
            var faction = await _db.Factions
                .FirstOrDefaultAsync(f => f.EventId == eventId);

            if (faction != null)
            {
                commandLevel = new FrequencyLevelDto(faction.PrimaryFrequency, faction.BackupFrequency);

                if (eventPlayer?.Squad != null)
                    squadLevel = new FrequencyLevelDto(eventPlayer.Squad.PrimaryFrequency, eventPlayer.Squad.BackupFrequency);

                if (eventPlayer?.Platoon != null)
                    platoonLevel = new FrequencyLevelDto(eventPlayer.Platoon.PrimaryFrequency, eventPlayer.Platoon.BackupFrequency);
                else if (eventPlayer?.Squad?.Platoon != null)
                    platoonLevel = new FrequencyLevelDto(eventPlayer.Squad.Platoon.PrimaryFrequency, eventPlayer.Squad.Platoon.BackupFrequency);
            }
        }

        return new EventFrequenciesDto(squadLevel, platoonLevel, commandLevel);
    }

    // ── PUT /api/squads/{squadId}/frequencies ─────────────────────────────────

    public async Task<FrequencyLevelDto> SetSquadFrequenciesAsync(Guid squadId, SetFrequenciesRequest request)
    {
        var squad = await _db.Squads
            .Include(s => s.Platoon)
                .ThenInclude(p => p.Faction)
            .FirstOrDefaultAsync(s => s.Id == squadId)
            ?? throw new KeyNotFoundException($"Squad {squadId} not found");

        AssertCommanderAccess(squad.Platoon.Faction);

        squad.PrimaryFrequency = request.PrimaryFrequency;
        squad.BackupFrequency = request.BackupFrequency;
        await _db.SaveChangesAsync();

        return new FrequencyLevelDto(squad.PrimaryFrequency, squad.BackupFrequency);
    }

    // ── PUT /api/platoons/{platoonId}/frequencies ─────────────────────────────

    public async Task<FrequencyLevelDto> SetPlatoonFrequenciesAsync(Guid platoonId, SetFrequenciesRequest request)
    {
        var platoon = await _db.Platoons
            .Include(p => p.Faction)
            .FirstOrDefaultAsync(p => p.Id == platoonId)
            ?? throw new KeyNotFoundException($"Platoon {platoonId} not found");

        AssertCommanderAccess(platoon.Faction);

        platoon.PrimaryFrequency = request.PrimaryFrequency;
        platoon.BackupFrequency = request.BackupFrequency;
        await _db.SaveChangesAsync();

        return new FrequencyLevelDto(platoon.PrimaryFrequency, platoon.BackupFrequency);
    }

    // ── PUT /api/factions/{factionId}/frequencies ─────────────────────────────

    public async Task<FrequencyLevelDto> SetFactionFrequenciesAsync(Guid factionId, SetFrequenciesRequest request)
    {
        var faction = await _db.Factions
            .FirstOrDefaultAsync(f => f.Id == factionId)
            ?? throw new KeyNotFoundException($"Faction {factionId} not found");

        AssertCommanderAccess(faction);

        faction.PrimaryFrequency = request.PrimaryFrequency;
        faction.BackupFrequency = request.BackupFrequency;
        await _db.SaveChangesAsync();

        return new FrequencyLevelDto(faction.PrimaryFrequency, faction.BackupFrequency);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void AssertCommanderAccess(Data.Entities.Faction faction)
    {
        if (faction.CommanderId != _currentUser.UserId)
            throw new ForbiddenException("User is not the commander of this event's faction");
    }
}
