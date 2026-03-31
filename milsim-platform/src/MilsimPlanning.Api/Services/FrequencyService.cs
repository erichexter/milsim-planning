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
        _db          = db;
        _currentUser = currentUser;
    }

    // ── GET /api/events/{eventId}/frequencies ─────────────────────────────────

    public async Task<FrequencyDto> GetFrequenciesAsync(Guid eventId)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var ep = await _db.EventPlayers
            .Include(e => e.Squad)
                .ThenInclude(s => s!.Platoon)
                    .ThenInclude(p => p!.Faction)
            .Include(e => e.Platoon)
                .ThenInclude(p => p!.Faction)
            .FirstOrDefaultAsync(e => e.EventId == eventId && e.UserId == _currentUser.UserId);

        // Player/squad_leader/platoon_leader must have an EventPlayer record
        if (ep is null
            && _currentUser.Role != AppRoles.FactionCommander
            && _currentUser.Role != AppRoles.SystemAdmin)
        {
            throw new KeyNotFoundException($"EventPlayer for event {eventId} not found");
        }

        var faction = await _db.Factions.FirstOrDefaultAsync(f => f.EventId == eventId);

        return _currentUser.Role switch
        {
            AppRoles.Player => new FrequencyDto
            {
                Squad   = ToLevelDto(ep?.Squad),
                Platoon = null,
                Command = null
            },
            AppRoles.SquadLeader => new FrequencyDto
            {
                Squad   = ToLevelDto(ep?.Squad),
                Platoon = ToLevelDto(ep?.Squad?.Platoon),
                Command = null
            },
            AppRoles.PlatoonLeader => new FrequencyDto
            {
                Squad   = null,
                Platoon = ToLevelDto(ep?.Platoon),
                Command = ToLevelDto(faction)
            },
            _ => new FrequencyDto // faction_commander, system_admin
            {
                Squad   = null,
                Platoon = null,
                Command = ToLevelDto(faction)
            }
        };
    }

    // ── PUT /api/events/{eventId}/squads/{squadId}/frequencies ────────────────

    public async Task<FrequencyLevelDto> UpdateSquadFrequencyAsync(
        Guid eventId, Guid squadId, string? primary, string? backup)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var squad = await _db.Squads
            .Include(s => s.Platoon)
                .ThenInclude(p => p.Faction)
            .FirstOrDefaultAsync(s => s.Id == squadId)
            ?? throw new KeyNotFoundException($"Squad {squadId} not found");

        if (squad.Platoon.Faction.EventId != eventId)
            throw new ForbiddenException($"Squad {squadId} does not belong to event {eventId}");

        squad.SquadPrimaryFrequency = primary;
        squad.SquadBackupFrequency  = backup;
        await _db.SaveChangesAsync();

        return ToLevelDto(squad)!;
    }

    // ── PUT /api/events/{eventId}/platoons/{platoonId}/frequencies ────────────

    public async Task<FrequencyLevelDto> UpdatePlatoonFrequencyAsync(
        Guid eventId, Guid platoonId, string? primary, string? backup)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var platoon = await _db.Platoons
            .Include(p => p.Faction)
            .FirstOrDefaultAsync(p => p.Id == platoonId)
            ?? throw new KeyNotFoundException($"Platoon {platoonId} not found");

        if (platoon.Faction.EventId != eventId)
            throw new ForbiddenException($"Platoon {platoonId} does not belong to event {eventId}");

        platoon.PlatoonPrimaryFrequency = primary;
        platoon.PlatoonBackupFrequency  = backup;
        await _db.SaveChangesAsync();

        return ToLevelDto(platoon)!;
    }

    // ── PUT /api/events/{eventId}/command-frequencies ─────────────────────────

    public async Task<FrequencyLevelDto> UpdateCommandFrequencyAsync(
        Guid eventId, string? primary, string? backup)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var faction = await _db.Factions.FirstOrDefaultAsync(f => f.EventId == eventId)
            ?? throw new KeyNotFoundException($"Faction for event {eventId} not found");

        faction.CommandPrimaryFrequency = primary;
        faction.CommandBackupFrequency  = backup;
        await _db.SaveChangesAsync();

        return ToLevelDto(faction)!;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static FrequencyLevelDto? ToLevelDto(Squad? s) =>
        new() { Primary = s?.SquadPrimaryFrequency, Backup = s?.SquadBackupFrequency };

    private static FrequencyLevelDto? ToLevelDto(Platoon? p) =>
        new() { Primary = p?.PlatoonPrimaryFrequency, Backup = p?.PlatoonBackupFrequency };

    private static FrequencyLevelDto? ToLevelDto(Faction? f) =>
        f is null ? null : new() { Primary = f.CommandPrimaryFrequency, Backup = f.CommandBackupFrequency };
}
