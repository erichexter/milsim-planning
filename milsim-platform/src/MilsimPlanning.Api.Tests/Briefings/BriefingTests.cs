using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Models.Briefings;
using MilsimPlanning.Api.Services;
using MilsimPlanning.Api.Tests.Fixtures;
using Moq;
using Xunit;

namespace MilsimPlanning.Api.Tests.Briefings;

/// <summary>
/// Integration tests for the Briefing Board API — Story 1: Create Briefing Channel.
/// Tests run against a real PostgreSQL instance via Testcontainers.
/// </summary>
public class BriefingTestsBase : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    protected readonly PostgreSqlFixture _fixture;
    protected WebApplicationFactory<Program> _factory = null!;
    protected Mock<IEmailService> _emailMock = null!;

    // BriefingAdmin client — authorized for POST /api/v1/briefings
    protected HttpClient _briefingAdminClient = null!;
    protected AppUser _briefingAdminUser = null!;

    // Player client — used to test 403
    protected HttpClient _playerClient = null!;

    public BriefingTestsBase(PostgreSqlFixture fixture)
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
                builder.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Jwt:Secret"] = "dev-placeholder-secret-32-chars!!",
                        ["Jwt:Issuer"] = "milsim-tests",
                        ["Jwt:Audience"] = "milsim-tests"
                    });
                });

                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<AppDbContext>>();
                    services.RemoveAll<AppDbContext>();
                    services.AddDbContext<AppDbContext>(opts =>
                        opts.UseNpgsql(_fixture.ConnectionString));

                    services.RemoveAll<IEmailService>();
                    services.AddSingleton(_emailMock.Object);
                    services.AddSingleton(_emailMock);

                    services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = IntegrationTestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = IntegrationTestAuthHandler.SchemeName;
                    }).AddScheme<AuthenticationSchemeOptions, IntegrationTestAuthHandler>(IntegrationTestAuthHandler.SchemeName, _ => { });
                });
            });

        // Apply migrations
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        // Ensure roles exist
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "player", "squad_leader", "platoon_leader", "faction_commander", "system_admin", "briefing_admin" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // Create a briefing_admin user
        _briefingAdminUser = await CreateUserAsync($"briefing-admin-{Guid.NewGuid():N}@test.com", "briefing_admin");
        _briefingAdminClient = CreateAuthenticatedClient(_briefingAdminUser, "briefing_admin");

        // Create a player user (for 403 tests)
        var playerUser = await CreateUserAsync($"player-{Guid.NewGuid():N}@test.com", "player");
        _playerClient = CreateAuthenticatedClient(playerUser, "player");
    }

    public Task DisposeAsync()
    {
        _briefingAdminClient.Dispose();
        _playerClient.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    protected async Task<AppUser> CreateUserAsync(string email, string role, string password = "TestPass123!")
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
            Callsign = email,
            DisplayName = email,
            User = user
        };
        await db.SaveChangesAsync();
        return user;
    }

    protected HttpClient CreateAuthenticatedClient(AppUser user, string role)
    {
        var client = _factory.CreateClient();
        IntegrationTestAuthHandler.ApplyTestIdentity(client, user.Id, role);
        return client;
    }
}

// ── AC-01: POST /api/v1/briefings creates a Briefing ─────────────────────────

[Trait("Category", "Briefing_Create")]
public class BriefingCreateTests : BriefingTestsBase
{
    public BriefingCreateTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CreateBriefing_ValidRequest_Returns201WithChannelIdentifier()
    {
        var response = await _briefingAdminClient.PostAsJsonAsync("/api/v1/briefings",
            new { title = "Op Nightfall Brief", description = "Night operation briefing" });

        response.StatusCode.Should().Be(HttpStatusCode.Created, because: "AC-01: POST returns 201");

        var dto = await response.Content.ReadFromJsonAsync<BriefingDto>();
        dto.Should().NotBeNull();
        dto!.Id.Should().NotBeEmpty();
        dto.Title.Should().Be("Op Nightfall Brief");
        dto.Description.Should().Be("Night operation briefing");
        dto.PublicationState.Should().Be("Draft", because: "AC-03: publicationState defaults to Draft");
        dto.VersionETag.Should().Be("etag-v1", because: "AC-03: versionETag initialized to etag-v1");
        dto.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
        dto.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task CreateBriefing_ValidRequest_ChannelIdentifierIsUuidFormat()
    {
        // AC-01: channelIdentifier is auto-generated UUID
        var response = await _briefingAdminClient.PostAsJsonAsync("/api/v1/briefings",
            new { title = "UUID Test Brief" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var dto = await response.Content.ReadFromJsonAsync<BriefingDto>();
        dto.Should().NotBeNull();

        // channelIdentifier must parse as a valid Guid (UUID format)
        Guid.TryParse(dto!.ChannelIdentifier, out _)
            .Should().BeTrue(because: "AC-01: channelIdentifier must be a valid UUID");
        dto.ChannelIdentifier.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateBriefing_TwoIdenticalRequests_ProduceDifferentChannelIdentifiers()
    {
        // AC-04: Duplicate title+description attempts create SEPARATE briefings with different channelIdentifiers
        var requestBody = new { title = "Identical Brief", description = "Same description" };

        var response1 = await _briefingAdminClient.PostAsJsonAsync("/api/v1/briefings", requestBody);
        var response2 = await _briefingAdminClient.PostAsJsonAsync("/api/v1/briefings", requestBody);

        response1.StatusCode.Should().Be(HttpStatusCode.Created);
        response2.StatusCode.Should().Be(HttpStatusCode.Created);

        var dto1 = await response1.Content.ReadFromJsonAsync<BriefingDto>();
        var dto2 = await response2.Content.ReadFromJsonAsync<BriefingDto>();

        dto1!.Id.Should().NotBe(dto2!.Id, because: "two briefings must have different IDs");
        dto1.ChannelIdentifier.Should().NotBe(dto2.ChannelIdentifier,
            because: "AC-04: duplicate title+description creates separate briefings with DIFFERENT channelIdentifiers");
    }

    [Fact]
    public async Task CreateBriefing_MissingTitle_Returns400()
    {
        var response = await _briefingAdminClient.PostAsJsonAsync("/api/v1/briefings",
            new { title = "", description = "No title" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, because: "missing/empty title must return 400");
    }

    [Fact]
    public async Task CreateBriefing_WithoutAuth_Returns401()
    {
        var anonClient = _factory.CreateClient();
        var response = await anonClient.PostAsJsonAsync("/api/v1/briefings",
            new { title = "Anon Brief" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "unauthenticated requests must be rejected");
    }

    [Fact]
    public async Task CreateBriefing_PlayerRole_Returns403()
    {
        // AC: only BriefingAdmin can create briefings
        var response = await _playerClient.PostAsJsonAsync("/api/v1/briefings",
            new { title = "Player Brief" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "player role must not be able to create briefings");
    }

    [Fact]
    public async Task CreateBriefing_DescriptionIsOptional_Returns201()
    {
        var response = await _briefingAdminClient.PostAsJsonAsync("/api/v1/briefings",
            new { title = "Brief Without Description" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var dto = await response.Content.ReadFromJsonAsync<BriefingDto>();
        dto!.Description.Should().BeNull(because: "description is optional");
    }
}

// ── AC-02: channelIdentifier is immutable ────────────────────────────────────

[Trait("Category", "Briefing_ChannelIdentifier")]
public class BriefingChannelIdentifierTests : BriefingTestsBase
{
    public BriefingChannelIdentifierTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CreateBriefing_ChannelIdentifierStoredInDatabase_IsUnique()
    {
        // AC-02: channelIdentifier is unique across all briefings
        var response1 = await _briefingAdminClient.PostAsJsonAsync("/api/v1/briefings",
            new { title = "Brief A" });
        var response2 = await _briefingAdminClient.PostAsJsonAsync("/api/v1/briefings",
            new { title = "Brief B" });

        var dto1 = await response1.Content.ReadFromJsonAsync<BriefingDto>();
        var dto2 = await response2.Content.ReadFromJsonAsync<BriefingDto>();

        dto1!.ChannelIdentifier.Should().NotBe(dto2!.ChannelIdentifier,
            because: "AC-02: channelIdentifier must be unique across all briefings");

        // Verify in DB that both exist and have distinct channel identifiers
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var b1 = await db.Briefings.FirstOrDefaultAsync(b => b.Id == dto1.Id);
        var b2 = await db.Briefings.FirstOrDefaultAsync(b => b.Id == dto2.Id);

        b1.Should().NotBeNull();
        b2.Should().NotBeNull();
        b1!.ChannelIdentifier.Should().NotBe(b2!.ChannelIdentifier);
    }

    [Fact]
    public async Task CreateBriefing_DatabaseSchema_HasExpectedColumns()
    {
        // AC-05: verify DB has all required columns by checking the entity is persisted correctly
        var response = await _briefingAdminClient.PostAsJsonAsync("/api/v1/briefings",
            new { title = "Schema Test Brief", description = "Verify columns" });

        var dto = await response.Content.ReadFromJsonAsync<BriefingDto>();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var entity = await db.Briefings.FirstOrDefaultAsync(b => b.Id == dto!.Id);
        entity.Should().NotBeNull();
        entity!.Id.Should().NotBeEmpty();
        entity.Title.Should().Be("Schema Test Brief");
        entity.Description.Should().Be("Verify columns");
        entity.ChannelIdentifier.Should().NotBeNullOrEmpty();
        entity.PublicationState.Should().Be("Draft");
        entity.VersionETag.Should().Be("etag-v1");
        entity.IsDeleted.Should().BeFalse();
        entity.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
        entity.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }
}
