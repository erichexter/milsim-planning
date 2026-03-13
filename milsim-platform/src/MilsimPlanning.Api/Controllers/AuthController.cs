using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Models.Requests;
using MilsimPlanning.Api.Services;

namespace MilsimPlanning.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly MagicLinkService _magicLinkService;
    private readonly UserManager<AppUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;

    public AuthController(
        AuthService authService,
        MagicLinkService magicLinkService,
        UserManager<AppUser> userManager,
        IEmailService emailService,
        IConfiguration config)
    {
        _authService = authService;
        _magicLinkService = magicLinkService;
        _userManager = userManager;
        _emailService = emailService;
        _config = config;
    }

    // POST /api/auth/login
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var outcome = await _authService.LoginAsync(request.Email, request.Password);
        return outcome.Result switch
        {
            LoginResult.Success => Ok(outcome.Response),
            LoginResult.LockedOut => StatusCode(429, new { error = "Account locked out due to too many failed attempts" }),
            _ => Unauthorized(new { error = "Invalid credentials" })
        };
    }

    // POST /api/auth/logout
    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout() => Ok(); // JWT is stateless; client discards token

    // POST /api/auth/magic-link (request link)
    [HttpPost("magic-link")]
    [AllowAnonymous]
    public async Task<IActionResult> RequestMagicLink(MagicLinkRequest request)
    {
        await _magicLinkService.SendMagicLinkAsync(request.Email);
        return Ok(new { message = "If this email is registered, a sign-in link has been sent." });
    }

    // GET /api/auth/magic-link/confirm (landing page — must NOT auto-login)
    [HttpGet("magic-link/confirm")]
    [AllowAnonymous]
    public IActionResult MagicLinkLanding([FromQuery] string token, [FromQuery] string userId)
    {
        // Return HTML with a button — email scanner protection
        // Email scanners follow GET links but won't submit POST forms
        var html = $"""
            <!DOCTYPE html>
            <html>
            <head><title>Complete Sign-In</title></head>
            <body>
              <h1>Complete Sign-In</h1>
              <p>Click the button below to sign in to Milsim Platform.</p>
              <form method="post" action="/api/auth/magic-link/confirm">
                <input type="hidden" name="token" value="{System.Net.WebUtility.HtmlEncode(token)}" />
                <input type="hidden" name="userId" value="{System.Net.WebUtility.HtmlEncode(userId)}" />
                <button type="submit">Click to sign in</button>
              </form>
            </body>
            </html>
            """;
        return Content(html, "text/html");
    }

    // POST /api/auth/magic-link/confirm (completes auth)
    [HttpPost("magic-link/confirm")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmMagicLink([FromForm] string token, [FromForm] string userId)
    {
        var result = await _magicLinkService.VerifyMagicLinkAsync(userId, token);
        if (result is null) return Unauthorized(new { error = "Invalid or expired magic link" });
        return Ok(result);
    }

    // POST /api/auth/password-reset (request reset email)
    [HttpPost("password-reset")]
    [AllowAnonymous]
    public async Task<IActionResult> RequestPasswordReset(PasswordResetRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is not null)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var appUrl = _config["AppUrl"] ?? "http://localhost:5173";
            await _emailService.SendAsync(
                request.Email,
                "Reset your password",
                $"Reset link: {appUrl}/auth/reset-password?userId={user.Id}&token={encoded}");
        }
        return Ok(new { message = "If this email is registered, a password reset link has been sent." });
    }

    // POST /api/auth/password-reset/confirm
    [HttpPost("password-reset/confirm")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmPasswordReset(ConfirmPasswordResetRequest request)
    {
        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user is null) return BadRequest(new { error = "Invalid request" });

        var decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.Token));
        var result = await _userManager.ResetPasswordAsync(user, decoded, request.NewPassword);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        return Ok(new { message = "Password updated successfully" });
    }

    // POST /api/auth/invite
    [HttpPost("invite")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<IActionResult> InviteUser(InviteUserRequest request)
    {
        var user = await _authService.InviteUserAsync(request);
        return CreatedAtAction(null, new { id = user.Id });
    }
}
