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
/// Integration tests for Story 2: GET /api/v1/briefings — Admin Dashboard Channel List.
/// Tests run against a real PostgreSQL instance via Testcontainers.
/// </summary>
public class BriefingListTestsBase : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    protected readonly PostgreSqlFixture _fixture;
    protected WebApplicationFactory<Program> _factory = null!;
    protected Mock<IEmailService> _emailMock = null!;
    protected HttpClient _briefingAdminClient = null!;
    protected HttpClient _playerClient = null!;
    protected AppUser _briefingAdminUser = null!;

    public BriefingListTestsBase(PostgreSqlFixture fixture)
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

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "player", "squad_leader", "platoon_leader", "faction_commander", "system_admin", "briefing_admin" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        _briefingAdminUser = await CreateUserAsync($"list-admin-{Guid.NewGuid():N}@test.com", "briefing_admin");
        _briefingAdminClient = CreateAuthenticatedClient(_briefingAdminUser, "briefing_admin");

        var playerUser = await CreateUserAsync($"list-player-{Guid.NewGuid():N}@test.com", "player");
        _playerClient = CreateAuthenticatedClient(playerUser, "player");
    }

    public Task DisposeAsync()
    {
        _briefingAdminClient.Dispose();
        _playerClient.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

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

    /// <summary>Creates a briefing via POST and returns the response DTO.</summary>
    protected async Task<BriefingDto> CreateBriefingAsync(string title, string? description = null)
    {
        var response = await _briefingAdminClient.PostAsJsonAsync("/api/v1/briefings",
            new { title, description });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BriefingDto>())!;
    }
}

// ── AC-01: GET /api/v1/briefings returns paginated list ──────────────────────

[Trait("Category", "BriefingList_Happy")]
public class BriefingListHappyPathTests : BriefingListTestsBase
{
    public BriefingListHappyPathTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ListBriefings_EmptyDb_Returns200WithEmptyItems()
    {
        var response = await _briefingAdminClient.GetAsync("/api/v1/briefings");

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: "AC-01: GET returns 200");

        var result = await response.Content.ReadFromJsonAsync<BriefingListDto>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeNull();
        result.Pagination.Should().NotBeNull();
        result.Pagination.Limit.Should().Be(20);
        result.Pagination.Offset.Should().Be(0);
    }

    [Fact]
    public async Task ListBriefings_WithThreeBriefings_ReturnsTotalThree()
    {
        // AC-02: Create 3 briefings, GET → total=3, items.length=3
        await CreateBriefingAsync("Brief Alpha", "Description A");
        await CreateBriefingAsync("Brief Beta", "Description B");
        await CreateBriefingAsync("Brief Gamma");

        var response = await _briefingAdminClient.GetAsync("/api/v1/briefings");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<BriefingListDto>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeNull();

        // At minimum our 3 briefings must be present (other tests may have added more)
        result.Pagination.Total.Should().BeGreaterThanOrEqualTo(3,
            because: "AC-02: total must reflect all non-deleted briefings");

        var items = result.Items.ToList();
        items.Should().AllSatisfy(item =>
        {
            item.Id.Should().NotBeEmpty();
            item.Title.Should().NotBeNullOrEmpty();
            item.ChannelIdentifier.Should().NotBeNullOrEmpty();
            item.PublicationState.Should().NotBeNullOrEmpty();
        });
    }

    [Fact]
    public async Task ListBriefings_ResponseShape_HasAllRequiredFields()
    {
        // AC-01: response includes title, description, channelIdentifier, publicationState, updatedAt
        await CreateBriefingAsync("Shape Test Brief", "Shape Description");

        var response = await _briefingAdminClient.GetAsync("/api/v1/briefings");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<BriefingListDto>();
        result.Should().NotBeNull();

        var match = result!.Items.FirstOrDefault(i => i.Title == "Shape Test Brief");
        match.Should().NotBeNull(because: "the created briefing must appear in the list");
        match!.Title.Should().Be("Shape Test Brief");
        match.Description.Should().Be("Shape Description");
        Guid.TryParse(match.ChannelIdentifier, out _).Should().BeTrue(because: "channelIdentifier must be a valid UUID");
        match.PublicationState.Should().Be("Draft");
        match.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task ListBriefings_DefaultPagination_LimitTwentyOffsetZero()
    {
        // AC-05: defaults to limit=20, offset=0
        var response = await _briefingAdminClient.GetAsync("/api/v1/briefings");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<BriefingListDto>();
        result!.Pagination.Limit.Should().Be(20);
        result.Pagination.Offset.Should().Be(0);
    }

    [Fact]
    public async Task ListBriefings_WithPaginationParams_ReturnsCorrectSlice()
    {
        // AC-05: Pagination query params (limit=2, offset=1) → returns correct slice
        // Create 3 known briefings (in practice there may be more from other tests)
        await CreateBriefingAsync("Paginate A");
        await CreateBriefingAsync("Paginate B");
        await CreateBriefingAsync("Paginate C");

        // Get total first
        var allResponse = await _briefingAdminClient.GetAsync("/api/v1/briefings?limit=100&offset=0");
        allResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var allResult = await allResponse.Content.ReadFromJsonAsync<BriefingListDto>();
        var totalCount = allResult!.Pagination.Total;

        // Now get with limit=2, offset=1
        var sliceResponse = await _briefingAdminClient.GetAsync("/api/v1/briefings?limit=2&offset=1");
        sliceResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var sliceResult = await sliceResponse.Content.ReadFromJsonAsync<BriefingListDto>();
        sliceResult.Should().NotBeNull();
        sliceResult!.Pagination.Limit.Should().Be(2);
        sliceResult.Pagination.Offset.Should().Be(1);
        sliceResult.Pagination.Total.Should().Be(totalCount, because: "total reflects the full set, not just the page");
        sliceResult.Items.Count().Should().BeLessOrEqualTo(2, because: "limit=2 means at most 2 items returned");
    }
}

// ── Authorization tests ──────────────────────────────────────────────────────

[Trait("Category", "BriefingList_Auth")]
public class BriefingListAuthTests : BriefingListTestsBase
{
    public BriefingListAuthTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ListBriefings_PlayerRole_Returns403()
    {
        var response = await _playerClient.GetAsync("/api/v1/briefings");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "player role must not access the briefing list");
    }

    [Fact]
    public async Task ListBriefings_Unauthenticated_Returns401()
    {
        var anonClient = _factory.CreateClient();
        var response = await anonClient.GetAsync("/api/v1/briefings");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "unauthenticated requests must be rejected");
    }
}

// ── Soft-delete exclusion test ───────────────────────────────────────────────

[Trait("Category", "BriefingList_SoftDelete")]
public class BriefingListSoftDeleteTests : BriefingListTestsBase
{
    public BriefingListSoftDeleteTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ListBriefings_SoftDeletedBriefings_AreExcluded()
    {
        // AC: Soft-deleted briefings are excluded from results
        await CreateBriefingAsync("Visible Brief");

        // Directly mark one briefing as deleted in DB
        var deletedId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Briefings.Add(new Briefing
            {
                Id = deletedId,
                Title = "Soft Deleted Brief",
                ChannelIdentifier = Guid.NewGuid().ToString(),
                PublicationState = "Draft",
                VersionETag = "etag-v1",
                IsDeleted = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var response = await _briefingAdminClient.GetAsync("/api/v1/briefings?limit=100&offset=0");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<BriefingListDto>();
        result!.Items.Should().NotContain(i => i.Id == deletedId,
            because: "soft-deleted briefings must not appear in the list");
    }
}
