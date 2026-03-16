using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Services;

namespace MilsimPlanning.Api.Controllers;

[ApiController]
[Route("api/events/{eventId:guid}")]
public class PlayerController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public PlayerController(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    /// <summary>GET /my-assignment — player views their platoon/squad/callsign</summary>
    [HttpGet("my-assignment")]
    [Authorize(Policy = "RequirePlayer")]
    public async Task<IActionResult> GetMyAssignment(Guid eventId)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        // Direct query by UserId+EventId — NOT roster hierarchy walk (Pitfall 4 / anti-pattern)
        var player = await _db.EventPlayers
            .Include(ep => ep.Squad)
                .ThenInclude(s => s!.Platoon)
            .Include(ep => ep.Platoon)
            .FirstOrDefaultAsync(ep => ep.EventId == eventId && ep.UserId == _currentUser.UserId);

        if (player is null) return NotFound();

        // Prefer the user's profile callsign if they've set one; fall back to roster callsign.
        var profile = await _db.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == _currentUser.UserId);
        var displayCallsign = !string.IsNullOrWhiteSpace(profile?.Callsign)
            ? profile.Callsign
            : player.Callsign;

        return Ok(new
        {
            player.Id,
            player.Name,
            Callsign = displayCallsign,
            player.TeamAffiliation,
            player.Role,
            Platoon = player.Platoon is null ? null : new { player.Platoon.Id, player.Platoon.Name },
            Squad = player.Squad is null ? null : new { player.Squad.Id, player.Squad.Name },
            IsAssigned = player.SquadId is not null
        });
    }
}
