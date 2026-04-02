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

    // ── FREQ-01: GET squad frequency ──────────────────────────────────────────

    public async Task<SquadFrequencyDto> GetSquadFrequencyAsync(Guid squadId)
    {
        var squad = await _db.Squads
            .Include(s => s.Platoon).ThenInclude(p => p.Faction).ThenInclude(f => f.Event)
            .FirstOrDefaultAsync(s => s.Id == squadId)
            ?? throw new KeyNotFoundException($"Squad {squadId} not found.");

        ScopeGuard.AssertEventAccess(_currentUser, squad.Platoon.Faction.EventId);

        var ep = await GetEventPlayerAsync(squad.Platoon.Faction.EventId);

        var canRead = _currentUser.Role is AppRoles.FactionCommander or AppRoles.SystemAdmin
            || (_currentUser.Role == AppRoles.PlatoonLeader && ep?.PlatoonId == squad.PlatoonId)
            || (_currentUser.Role is AppRoles.SquadLeader or AppRoles.Player && ep?.SquadId == squadId);
        if (!canRead) throw new ForbiddenException("Insufficient role to access this resource.");

        return new SquadFrequencyDto
        {
            SquadId = squad.Id,
            Primary = squad.PrimaryFrequency,
            Backup = squad.BackupFrequency
        };
    }

    // ── FREQ-02: PATCH squad frequency ───────────────────────────────────────

    public async Task UpdateSquadFrequencyAsync(Guid squadId, UpdateFrequencyRequest request)
    {
        var squad = await _db.Squads
            .Include(s => s.Platoon).ThenInclude(p => p.Faction).ThenInclude(f => f.Event)
            .FirstOrDefaultAsync(s => s.Id == squadId)
            ?? throw new KeyNotFoundException($"Squad {squadId} not found.");

        ScopeGuard.AssertEventAccess(_currentUser, squad.Platoon.Faction.EventId);

        var ep = await GetEventPlayerAsync(squad.Platoon.Faction.EventId);

        var canWrite = _currentUser.Role is AppRoles.FactionCommander or AppRoles.SystemAdmin
            || (_currentUser.Role == AppRoles.PlatoonLeader && ep?.PlatoonId == squad.PlatoonId)
            || (_currentUser.Role == AppRoles.SquadLeader && ep?.SquadId == squadId);
        if (!canWrite) throw new ForbiddenException("Insufficient role to access this resource.");

        squad.PrimaryFrequency = request.Primary;
        squad.BackupFrequency = request.Backup;
        await _db.SaveChangesAsync();
    }

    // ── FREQ-03: GET platoon frequency ───────────────────────────────────────

    public async Task<PlatoonFrequencyDto> GetPlatoonFrequencyAsync(Guid platoonId)
    {
        var platoon = await _db.Platoons
            .Include(p => p.Faction).ThenInclude(f => f.Event)
            .FirstOrDefaultAsync(p => p.Id == platoonId)
            ?? throw new KeyNotFoundException($"Platoon {platoonId} not found.");

        ScopeGuard.AssertEventAccess(_currentUser, platoon.Faction.EventId);

        var ep = await GetEventPlayerAsync(platoon.Faction.EventId);

        var canRead = _currentUser.Role is AppRoles.FactionCommander or AppRoles.SystemAdmin
            || (_currentUser.Role == AppRoles.PlatoonLeader && ep?.PlatoonId == platoonId)
            || (_currentUser.Role == AppRoles.SquadLeader && ep?.PlatoonId == platoonId);
        if (!canRead) throw new ForbiddenException("Insufficient role to access this resource.");

        return new PlatoonFrequencyDto
        {
            PlatoonId = platoon.Id,
            Primary = platoon.PrimaryFrequency,
            Backup = platoon.BackupFrequency
        };
    }

    // ── FREQ-04: PATCH platoon frequency ─────────────────────────────────────

    public async Task UpdatePlatoonFrequencyAsync(Guid platoonId, UpdateFrequencyRequest request)
    {
        var platoon = await _db.Platoons
            .Include(p => p.Faction).ThenInclude(f => f.Event)
            .FirstOrDefaultAsync(p => p.Id == platoonId)
            ?? throw new KeyNotFoundException($"Platoon {platoonId} not found.");

        ScopeGuard.AssertEventAccess(_currentUser, platoon.Faction.EventId);

        var ep = await GetEventPlayerAsync(platoon.Faction.EventId);

        var canWrite = _currentUser.Role is AppRoles.FactionCommander or AppRoles.SystemAdmin
            || (_currentUser.Role == AppRoles.PlatoonLeader && ep?.PlatoonId == platoonId);
        if (!canWrite) throw new ForbiddenException("Insufficient role to access this resource.");

        platoon.PrimaryFrequency = request.Primary;
        platoon.BackupFrequency = request.Backup;
        await _db.SaveChangesAsync();
    }

    // ── FREQ-05: GET faction frequency ───────────────────────────────────────

    public async Task<FactionFrequencyDto> GetFactionFrequencyAsync(Guid factionId)
    {
        var faction = await _db.Factions
            .Include(f => f.Event)
            .FirstOrDefaultAsync(f => f.Id == factionId)
            ?? throw new KeyNotFoundException($"Faction {factionId} not found.");

        ScopeGuard.AssertEventAccess(_currentUser, faction.EventId);

        var ep = await GetEventPlayerAsync(faction.EventId);

        var platoonInFaction = ep?.PlatoonId != null
            && await PlatoonBelongsToFactionAsync(ep.PlatoonId.Value, factionId);

        var canRead = _currentUser.Role is AppRoles.FactionCommander or AppRoles.SystemAdmin
            || (_currentUser.Role == AppRoles.PlatoonLeader && platoonInFaction);
        if (!canRead) throw new ForbiddenException("Insufficient role to access this resource.");

        return new FactionFrequencyDto
        {
            FactionId = faction.Id,
            Primary = faction.PrimaryFrequency,
            Backup = faction.BackupFrequency
        };
    }

    // ── FREQ-06: PATCH faction frequency ─────────────────────────────────────

    public async Task UpdateFactionFrequencyAsync(Guid factionId, UpdateFrequencyRequest request)
    {
        var faction = await _db.Factions
            .Include(f => f.Event)
            .FirstOrDefaultAsync(f => f.Id == factionId)
            ?? throw new KeyNotFoundException($"Faction {factionId} not found.");

        ScopeGuard.AssertEventAccess(_currentUser, faction.EventId);

        var canWrite = _currentUser.Role is AppRoles.FactionCommander or AppRoles.SystemAdmin;
        if (!canWrite) throw new ForbiddenException("Insufficient role to access this resource.");

        faction.PrimaryFrequency = request.Primary;
        faction.BackupFrequency = request.Backup;
        await _db.SaveChangesAsync();
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private async Task<Data.Entities.EventPlayer?> GetEventPlayerAsync(Guid eventId)
    {
        return await _db.EventPlayers
            .FirstOrDefaultAsync(ep => ep.UserId == _currentUser.UserId && ep.EventId == eventId);
    }

    private async Task<bool> PlatoonBelongsToFactionAsync(Guid platoonId, Guid factionId)
    {
        return await _db.Platoons
            .Where(p => p.Id == platoonId)
            .Select(p => p.FactionId)
            .FirstOrDefaultAsync() == factionId;
    }
}
