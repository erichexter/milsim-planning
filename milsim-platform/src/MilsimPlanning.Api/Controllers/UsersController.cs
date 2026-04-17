using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MilsimPlanning.Api.Models.Users;
using MilsimPlanning.Api.Services;

namespace MilsimPlanning.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly UserService _userService;
    private readonly ICurrentUser _currentUser;

    public UsersController(UserService userService, ICurrentUser currentUser)
    {
        _userService = userService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Search for users by email/name for role assignment autocomplete.
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<List<UserSearchResultDto>>> Search([FromQuery] string? query, [FromQuery] int limit = 10)
    {
        if (limit < 1 || limit > 50)
            limit = 10;

        var results = await _userService.SearchUsersAsync(query, limit);
        return Ok(results);
    }

    /// <summary>
    /// Get current user's event memberships (for multi-event selector).
    /// </summary>
    [HttpGet("me/events")]
    public async Task<ActionResult<List<EventMembershipDto>>> GetMyEventMemberships()
    {
        var memberships = await _userService.GetUserEventMembershipsAsync(_currentUser.UserId);
        return Ok(memberships);
    }
}
