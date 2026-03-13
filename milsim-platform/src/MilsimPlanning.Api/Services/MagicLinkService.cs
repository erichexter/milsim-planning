using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Models.Responses;

namespace MilsimPlanning.Api.Services;

public class MagicLinkService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly AppDbContext _db;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;
    private readonly AuthService _authService;

    public MagicLinkService(
        UserManager<AppUser> userManager,
        AppDbContext db,
        IEmailService emailService,
        IConfiguration config,
        AuthService authService)
    {
        _userManager = userManager;
        _db = db;
        _emailService = emailService;
        _config = config;
        _authService = authService;
    }

    public async Task SendMagicLinkAsync(string email)
    {
        // Silent return if user not found — don't reveal user existence
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null) return;

        // Generate an Identity token for magic link purpose
        var token = await _userManager.GenerateUserTokenAsync(
            user, TokenOptions.DefaultProvider, "MagicLinkLogin");

        // Hash the raw token for storage
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        // Store magic link token record
        var magicToken = new MagicLinkToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            UsedAt = null
        };
        _db.MagicLinkTokens.Add(magicToken);
        await _db.SaveChangesAsync();

        // Build confirm URL — GET renders the landing page, POST completes auth
        var appUrl = _config["AppUrl"] ?? "http://localhost:5173";
        var confirmUrl = $"{appUrl}/auth/magic-link/confirm?token={Uri.EscapeDataString(token)}&userId={user.Id}";

        await _emailService.SendAsync(
            email,
            "Your sign-in link",
            $"<p><a href=\"{confirmUrl}\">Click here to sign in</a> (valid for 30 minutes).</p>"
        );
    }

    public async Task<AuthResponse?> VerifyMagicLinkAsync(string userId, string rawToken)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return null;

        // Verify Identity token signature first
        var isValid = await _userManager.VerifyUserTokenAsync(
            user, TokenOptions.DefaultProvider, "MagicLinkLogin", rawToken);
        if (!isValid) return null;

        // Hash to look up the record
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

        // Find unused, non-expired record
        var record = _db.MagicLinkTokens
            .FirstOrDefault(t =>
                t.UserId == userId &&
                t.TokenHash == tokenHash &&
                t.UsedAt == null);

        if (record is null) return null;
        if (record.ExpiresAt < DateTime.UtcNow) return null;

        // CRITICAL ORDER: Mark used BEFORE issuing JWT to prevent race condition
        record.UsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Invalidate other sessions by updating security stamp
        await _userManager.UpdateSecurityStampAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "player";

        var token = _authService.GenerateJwt(user, role);
        return new AuthResponse(token, 604800);
    }
}
