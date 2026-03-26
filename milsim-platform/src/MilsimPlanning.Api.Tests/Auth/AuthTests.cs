using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Services;
using MilsimPlanning.Api.Tests.Fixtures;
using Moq;
using Xunit;

namespace MilsimPlanning.Api.Tests.Auth;

public class AuthTests : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private Mock<IEmailService> _emailMock = null!;

    public AuthTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _emailMock = new Mock<IEmailService>();
        _emailMock.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(Task.CompletedTask);

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace the real DB with Testcontainers DB
                    services.RemoveAll<DbContextOptions<AppDbContext>>();
                    services.RemoveAll<AppDbContext>();
                    services.AddDbContext<AppDbContext>(opts =>
                        opts.UseNpgsql(_fixture.ConnectionString));

                    // Replace email service with mock
                    services.RemoveAll<IEmailService>();
                    services.AddSingleton(_emailMock.Object);
                    services.AddSingleton(_emailMock); // so tests can access mock for Verify()
                });
            });

        _client = _factory.CreateClient();

        // Apply migrations to test DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        // Ensure roles exist
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "player", "squad_leader", "platoon_leader", "faction_commander", "system_admin" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    // ── Helper: create a test user ────────────────────────────────────────────
    private async Task<AppUser> CreateTestUserAsync(string email, string password, string role = "player", string callsign = "TestUser")
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(user, password);
        result.Succeeded.Should().BeTrue(because: string.Join("; ", result.Errors.Select(e => e.Description)));

        await userManager.AddToRoleAsync(user, role);

        user.Profile = new UserProfile
        {
            UserId = user.Id,
            Callsign = callsign,
            DisplayName = callsign,
            User = user
        };
        await db.SaveChangesAsync();

        return user;
    }

    [Fact]
    [Trait("Category", "Auth_Login")]
    public async Task Login_WithValidCredentials_ReturnsJwtToken()
    {
        var email = $"login-valid-{Guid.NewGuid():N}@test.com";
        await CreateTestUserAsync(email, "TestPass123!");

        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "TestPass123!" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("token").GetString();
        token.Should().NotBeNullOrEmpty();

        // Verify JWT structure and claims
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub);
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email);
    }

    [Fact]
    [Trait("Category", "Auth_Login")]
    public async Task Login_WithInvalidPassword_Returns401()
    {
        var email = $"login-invalid-{Guid.NewGuid():N}@test.com";
        await CreateTestUserAsync(email, "TestPass123!");

        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongPassword!" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Auth_Lockout")]
    public async Task Login_After5FailedAttempts_Returns429()
    {
        var email = $"login-lockout-{Guid.NewGuid():N}@test.com";
        await CreateTestUserAsync(email, "TestPass123!");

        // 5 failed attempts (MaxFailedAccessAttempts = 5 in Program.cs)
        for (var i = 0; i < 5; i++)
        {
            await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongPass!" });
        }

        // 6th attempt should be locked out (429 Too Many Requests)
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongPass!" });
        ((int)response.StatusCode).Should().Be(429);
    }

    [Fact]
    [Trait("Category", "Auth_MagicLink")]
    public async Task MagicLink_RequestForValidEmail_SendsEmail()
    {
        var email = $"magic-send-{Guid.NewGuid():N}@test.com";
        await CreateTestUserAsync(email, "TestPass123!");

        var response = await _client.PostAsJsonAsync("/api/auth/magic-link", new { email });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _emailMock.Verify(e => e.SendAsync(
            email,
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    [Trait("Category", "Auth_MagicLink")]
    public async Task MagicLink_ValidToken_ReturnsJwt()
    {
        var email = $"magic-valid-{Guid.NewGuid():N}@test.com";
        var user = await CreateTestUserAsync(email, "TestPass123!");

        // Capture the magic link URL from email
        string? capturedHtmlBody = null;
        _emailMock.Setup(e => e.SendAsync(email, It.IsAny<string>(), It.IsAny<string>()))
                  .Callback<string, string, string>((_, _, body) => capturedHtmlBody = body)
                  .Returns(Task.CompletedTask);

        await _client.PostAsJsonAsync("/api/auth/magic-link", new { email });

        capturedHtmlBody.Should().NotBeNull();

        // Extract token and userId from the link in the email body
        var (token, userId) = ExtractMagicLinkParams(capturedHtmlBody!);
        token.Should().NotBeNullOrEmpty();
        userId.Should().Be(user.Id);

        // POST confirm to complete auth
        var formContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("token", token!),
            new KeyValuePair<string, string>("userId", userId!)
        });
        var response = await _client.PostAsync("/api/auth/magic-link/confirm", formContent);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var jwtToken = body.GetProperty("token").GetString();
        jwtToken.Should().NotBeNullOrEmpty();
        jwtToken!.Split('.').Length.Should().Be(3, because: "JWT must have 3 parts");
    }

    [Fact]
    [Trait("Category", "Auth_MagicLink_SingleUse")]
    public async Task MagicLink_TokenUsedTwice_Returns401()
    {
        var email = $"magic-singleuse-{Guid.NewGuid():N}@test.com";
        await CreateTestUserAsync(email, "TestPass123!");

        string? capturedHtmlBody = null;
        _emailMock.Setup(e => e.SendAsync(email, It.IsAny<string>(), It.IsAny<string>()))
                  .Callback<string, string, string>((_, _, body) => capturedHtmlBody = body)
                  .Returns(Task.CompletedTask);

        await _client.PostAsJsonAsync("/api/auth/magic-link", new { email });

        var (token, userId) = ExtractMagicLinkParams(capturedHtmlBody!);

        var formContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("token", token!),
            new KeyValuePair<string, string>("userId", userId!)
        });

        // First use — should succeed
        var firstResponse = await _client.PostAsync("/api/auth/magic-link/confirm",
            new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("token", token!),
                new KeyValuePair<string, string>("userId", userId!)
            }));
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Second use — should return 401 (token already used)
        var secondResponse = await _client.PostAsync("/api/auth/magic-link/confirm",
            new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("token", token!),
                new KeyValuePair<string, string>("userId", userId!)
            }));
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Auth_MagicLink_Expired")]
    public async Task MagicLink_ExpiredToken_Returns401()
    {
        var email = $"magic-expired-{Guid.NewGuid():N}@test.com";
        var user = await CreateTestUserAsync(email, "TestPass123!");

        string? capturedHtmlBody = null;
        _emailMock.Setup(e => e.SendAsync(email, It.IsAny<string>(), It.IsAny<string>()))
                  .Callback<string, string, string>((_, _, body) => capturedHtmlBody = body)
                  .Returns(Task.CompletedTask);

        await _client.PostAsJsonAsync("/api/auth/magic-link", new { email });

        var (token, userId) = ExtractMagicLinkParams(capturedHtmlBody!);

        // Manually expire the token in the DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var record = db.MagicLinkTokens.FirstOrDefault(t => t.UserId == user.Id);
        record.Should().NotBeNull();
        record!.ExpiresAt = DateTime.UtcNow.AddMinutes(-1); // set to past
        await db.SaveChangesAsync();

        var response = await _client.PostAsync("/api/auth/magic-link/confirm",
            new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("token", token!),
                new KeyValuePair<string, string>("userId", userId!)
            }));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Auth_PasswordReset")]
    public async Task PasswordReset_RequestForValidEmail_SendsEmail()
    {
        var email = $"reset-send-{Guid.NewGuid():N}@test.com";
        await CreateTestUserAsync(email, "TestPass123!");

        var response = await _client.PostAsJsonAsync("/api/auth/password-reset", new { email });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _emailMock.Verify(e => e.SendAsync(
            email,
            It.IsAny<string>(),
            It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    [Trait("Category", "Auth_PasswordReset")]
    public async Task PasswordReset_ValidToken_UpdatesPassword()
    {
        var email = $"reset-confirm-{Guid.NewGuid():N}@test.com";
        var user = await CreateTestUserAsync(email, "OldPass123!");

        // Get the password reset token directly from UserManager (bypassing email capture for simplicity)
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var dbUser = await userManager.FindByEmailAsync(email);
        var resetToken = await userManager.GeneratePasswordResetTokenAsync(dbUser!);
        var encodedToken = System.Text.Encoding.UTF8.GetBytes(resetToken);
        var base64Token = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(encodedToken);

        var response = await _client.PostAsJsonAsync("/api/auth/password-reset/confirm", new
        {
            userId = dbUser!.Id,
            token = base64Token,
            newPassword = "NewPass456!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify we can now login with the new password
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "NewPass456!" });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Auth_Logout")]
    public async Task Logout_AuthenticatedUser_Returns200()
    {
        var email = $"logout-{Guid.NewGuid():N}@test.com";
        await CreateTestUserAsync(email, "TestPass123!");

        // Login first to get JWT
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "TestPass123!" });
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = loginBody.GetProperty("token").GetString();

        // Call logout with bearer token
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Auth_Invitation")]
    public async Task Invitation_CreatesUserAndSendsEmail()
    {
        // Create a faction_commander user to perform the invite
        var commanderEmail = $"commander-{Guid.NewGuid():N}@test.com";
        await CreateTestUserAsync(commanderEmail, "TestPass123!", "faction_commander");

        // Login as commander
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new { email = commanderEmail, password = "TestPass123!" });
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = loginBody.GetProperty("token").GetString();

        // Send invite
        var inviteEmail = $"invited-{Guid.NewGuid():N}@test.com";
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/invite");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new
        {
            email = inviteEmail,
            callsign = "Bravo1",
            displayName = "Bravo One",
            role = "player"
        });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        _emailMock.Verify(e => e.SendAsync(
            inviteEmail,
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Once);

        // Verify user was created in DB
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var invitedUser = await userManager.FindByEmailAsync(inviteEmail);
        invitedUser.Should().NotBeNull();
    }

    // ── AC-1 through AC-5: Self-service registration tests ────────────────────

    [Fact]
    [Trait("Category", "Auth_Register")]
    public async Task Register_WithValidData_Returns200AndJwt()
    {
        var email = $"register-valid-{Guid.NewGuid():N}@test.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            displayName = "Test Commander",
            email,
            password = "TestPass123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("token").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("userId").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("email").GetString().Should().Be(email);
        body.GetProperty("displayName").GetString().Should().Be("Test Commander");
        body.GetProperty("role").GetString().Should().Be("faction_commander");

        // Verify JWT structure
        var token = body.GetProperty("token").GetString()!;
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub);
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == email);
    }

    [Fact]
    [Trait("Category", "Auth_Register")]
    public async Task Register_MissingDisplayName_Returns400()
    {
        var email = $"register-noname-{Guid.NewGuid():N}@test.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            displayName = (string?)null,
            email,
            password = "TestPass123!"
        });

        ((int)response.StatusCode).Should().BeOneOf(400);
    }

    [Fact]
    [Trait("Category", "Auth_Register")]
    public async Task Register_ShortPassword_Returns400()
    {
        var email = $"register-shortpw-{Guid.NewGuid():N}@test.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            displayName = "Test User",
            email,
            password = "abc"
        });

        ((int)response.StatusCode).Should().Be(400);
    }

    [Fact]
    [Trait("Category", "Auth_Register")]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var email = $"register-dup-{Guid.NewGuid():N}@test.com";

        // First registration should succeed
        var first = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            displayName = "First User",
            email,
            password = "TestPass123!"
        });
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        // Second registration with same email should return 409
        var second = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            displayName = "Second User",
            email,
            password = "TestPass456!"
        });
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var body = await second.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("error").GetString().Should().Contain("email already exists");
    }

    [Fact]
    [Trait("Category", "Auth_Register")]
    public async Task Register_SelfRegisteredUser_HasFactionCommanderRole()
    {
        var email = $"register-role-{Guid.NewGuid():N}@test.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            displayName = "Role Tester",
            email,
            password = "TestPass123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify role in DB
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await userManager.FindByEmailAsync(email);
        user.Should().NotBeNull();

        var roles = await userManager.GetRolesAsync(user!);
        roles.Should().Contain("faction_commander",
            because: "self-registered users must be assigned faction_commander role (AC-5)");
    }

    // ── Helper: extract token + userId from magic link email HTML ─────────────
    private static (string? token, string? userId) ExtractMagicLinkParams(string htmlBody)
    {
        // Extract from href: /auth/magic-link/confirm?token=...&userId=...
        var tokenMatch = System.Text.RegularExpressions.Regex.Match(htmlBody, @"token=([^&""<\s]+)");
        var userIdMatch = System.Text.RegularExpressions.Regex.Match(htmlBody, @"userId=([^&""<\s]+)");

        var token = tokenMatch.Success ? Uri.UnescapeDataString(tokenMatch.Groups[1].Value) : null;
        var userId = userIdMatch.Success ? Uri.UnescapeDataString(userIdMatch.Groups[1].Value) : null;

        return (token, userId);
    }
}
