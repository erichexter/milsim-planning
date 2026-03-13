using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Models.Requests;
using MilsimPlanning.Api.Models.Responses;

namespace MilsimPlanning.Api.Services;

public enum LoginResult { Success, InvalidCredentials, LockedOut }

public record LoginOutcome(LoginResult Result, AuthResponse? Response = null);

public class AuthService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly IConfiguration _config;
    private readonly IEmailService _emailService;

    public AuthService(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        IConfiguration config,
        IEmailService emailService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _config = config;
        _emailService = emailService;
    }

    public async Task<LoginOutcome> LoginAsync(string email, string password)
    {
        var result = await _signInManager.PasswordSignInAsync(
            email, password, isPersistent: false, lockoutOnFailure: true);

        if (result.IsLockedOut)
            return new LoginOutcome(LoginResult.LockedOut);

        if (!result.Succeeded)
            return new LoginOutcome(LoginResult.InvalidCredentials);

        var user = await _userManager.FindByEmailAsync(email);
        if (user is null) return new LoginOutcome(LoginResult.InvalidCredentials);

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "player";

        var token = GenerateJwt(user, role);
        return new LoginOutcome(LoginResult.Success, new AuthResponse(token, 604800)); // 7 days in seconds
    }

    public string GenerateJwt(AppUser user, string role)
    {
        var secret = _config["Jwt:Secret"] ?? "dev-placeholder-secret-32-chars!!";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new Claim(ClaimTypes.Role, role),
            new Claim("callsign", user.Profile?.Callsign ?? "")
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<AppUser> InviteUserAsync(InviteUserRequest request)
    {
        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = false
        };

        var tempPassword = Guid.NewGuid().ToString();
        var createResult = await _userManager.CreateAsync(user, tempPassword);
        if (!createResult.Succeeded)
            throw new InvalidOperationException(
                string.Join("; ", createResult.Errors.Select(e => e.Description)));

        // Create UserProfile
        user.Profile = new Data.Entities.UserProfile
        {
            UserId = user.Id,
            Callsign = request.Callsign,
            DisplayName = request.DisplayName,
            User = user
        };

        await _userManager.UpdateAsync(user);
        await _userManager.AddToRoleAsync(user, request.Role);

        var inviteToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var appUrl = _config["AppUrl"] ?? "http://localhost:5173";
        var activationLink = $"{appUrl}/auth/activate?userId={user.Id}&token={Uri.EscapeDataString(inviteToken)}";

        await _emailService.SendAsync(
            request.Email,
            "You've been invited to Milsim Platform",
            $"<p>You've been invited. <a href=\"{activationLink}\">Click here to activate your account.</a></p>"
        );

        return user;
    }
}
