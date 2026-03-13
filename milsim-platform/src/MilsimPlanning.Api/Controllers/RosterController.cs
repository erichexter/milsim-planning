using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Domain;
using MilsimPlanning.Api.Services;

namespace MilsimPlanning.Api.Controllers;

/// <summary>
/// Stub roster controller — Phase 2 will replace this with real roster data.
///
/// Demonstrates:
/// 1. RequirePlayer authorization policy (minimum role check via MinimumRoleHandler)
/// 2. ScopeGuard.AssertEventAccess (IDOR prevention for AUTHZ-06)
/// 3. Service-layer email projection (AUTHZ-05): email stripped for Player/SquadLeader callers
/// </summary>
[ApiController]
[Route("api/roster")]
[Authorize(Policy = "RequirePlayer")]
public class RosterController : ControllerBase
{
    private readonly ICurrentUser _currentUser;

    public RosterController(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    /// <summary>
    /// GET /api/roster/{eventId}
    ///
    /// Returns a mock roster for the event. Real implementation comes in Phase 2.
    /// - Players and SquadLeaders: email field is omitted from response (AUTHZ-05)
    /// - PlatoonLeader and above: email field is included
    /// </summary>
    [HttpGet("{eventId:guid}")]
    public IActionResult GetRoster(Guid eventId)
    {
        // IDOR check — must be called at top of every service method taking eventId
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        // Determine whether to include email based on role (AUTHZ-05)
        var callerLevel = AppRoles.Hierarchy.GetValueOrDefault(_currentUser.Role, 0);
        var platoonLeaderLevel = AppRoles.Hierarchy.GetValueOrDefault(AppRoles.PlatoonLeader, 0);
        var includeEmail = callerLevel >= platoonLeaderLevel;

        // Mock roster — Phase 2 replaces with real EventMembership query
        object[] users = includeEmail
            ? new object[]
              {
                  new { name = "Alpha One", email = "alpha1@example.com" },
                  new { name = "Bravo Two", email = "bravo2@example.com" }
              }
            : new object[]
              {
                  new { name = "Alpha One" },
                  new { name = "Bravo Two" }
              };

        return Ok(new { eventId, users });
    }
}
