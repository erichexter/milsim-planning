using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace MilsimPlanning.Api.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly UserManager<AppUser> _userManager;
    private readonly AppDbContext _db;

    public ProfileController(ICurrentUser currentUser, UserManager<AppUser> userManager, AppDbContext db)
    {
        _currentUser = currentUser;
        _userManager = userManager;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        var user = await _userManager.FindByIdAsync(_currentUser.UserId);
        if (user is null) return NotFound();

        var profile = await _db.Set<UserProfile>()
            .FirstOrDefaultAsync(p => p.UserId == user.Id);

        return Ok(new
        {
            email = user.Email,
            callsign = profile?.Callsign,
            displayName = profile?.DisplayName,
            role = _currentUser.Role,
        });
    }
}
