using FluentValidation;
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
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace MilsimPlanning.Api.Tests.Maps;

/// <summary>
/// Integration tests for Map Resources API (MAPS-01..05).
/// Requires Docker Desktop for Testcontainers PostgreSQL.
/// </summary>
public class MapResourceTestsBase : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    protected readonly PostgreSqlFixture _fixture;
    protected WebApplicationFactory<Program> _factory = null!;
    protected Mock<IEmailService> _emailMock = null!;
    protected Mock<IFileService> _fileServiceMock = null!;
    protected HttpClient _commanderClient = null!;
    protected HttpClient _playerClient = null!;
    protected Guid _eventId;

    public MapResourceTestsBase(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _emailMock = new Mock<IEmailService>();
        _emailMock.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(Task.CompletedTask);

        _fileServiceMock = new Mock<IFileService>();
        _fileServiceMock
            .Setup(f => f.GenerateUploadUrl(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((Guid eventId, Guid resourceId, string contentType, string fileName) =>
                new UploadUrlResponse(
                    Guid.NewGuid(),
                    $"https://fake.r2.com/upload/{resourceId}",
                    $"events/{eventId}/resources/{resourceId}/files/upload-123/{fileName}"
                ));
        _fileServiceMock
            .Setup(f => f.GenerateDownloadUrl(It.IsAny<string>()))
            .Returns("https://fake.r2.com/download/signed-url");

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<AppDbContext>>();
                    services.RemoveAll<AppDbContext>();
                    services.AddDbContext<AppDbContext>(opts =>
                        opts.UseNpgsql(_fixture.ConnectionString));

                    services.RemoveAll<IEmailService>();
                    services.AddSingleton(_emailMock.Object);

                    services.RemoveAll<IFileService>();
                    services.AddScoped<IFileService>(_ => _fileServiceMock.Object);
                });
            });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "player", "squad_leader", "platoon_leader", "faction_commander", "system_admin" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var commanderEmail = $"maps-cmdr-{Guid.NewGuid():N}@test.com";
        var commander = new AppUser { UserName = commanderEmail, Email = commanderEmail, EmailConfirmed = true };
        await userManager.CreateAsync(commander, "TestPass123!");
        await userManager.AddToRoleAsync(commander, "faction_commander");
        commander.Profile = new UserProfile { UserId = commander.Id, Callsign = "Commander", DisplayName = "Commander", User = commander };

        var playerEmail = $"maps-player-{Guid.NewGuid():N}@test.com";
        var player = new AppUser { UserName = playerEmail, Email = playerEmail, EmailConfirmed = true };
        await userManager.CreateAsync(player, "TestPass123!");
        await userManager.AddToRoleAsync(player, "player");
        player.Profile = new UserProfile { UserId = player.Id, Callsign = "Player1", DisplayName = "Player1", User = player };

        _eventId = Guid.NewGuid();
        var factionId = Guid.NewGuid();
        var faction = new Faction { Id = factionId, Name = "Test Faction", CommanderId = commander.Id, EventId = _eventId };
        var testEvent = new Event { Id = _eventId, Name = "Maps Test Event", Status = EventStatus.Draft, FactionId = factionId, Faction = faction };

        db.Events.Add(testEvent);
        db.EventMemberships.Add(new EventMembership { UserId = commander.Id, EventId = _eventId, Role = "faction_commander" });
        db.EventMemberships.Add(new EventMembership { UserId = player.Id, EventId = _eventId, Role = "player" });
        await db.SaveChangesAsync();

        var authService = scope.ServiceProvider.GetRequiredService<AuthService>();
        _commanderClient = _factory.CreateClient();
        _commanderClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authService.GenerateJwt(commander, "faction_commander"));
        _playerClient = _factory.CreateClient();
        _playerClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authService.GenerateJwt(player, "player"));
    }

    public Task DisposeAsync()
    {
        _commanderClient.Dispose();
        _playerClient.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    protected async Task<Guid> CreateExternalMapResourceAsync(string friendlyName = "Map Link", string? instructions = null)
    {
        var response = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/map-resources/external",
            new
            {
                ExternalUrl = "https://caltopo.example.com/map/123",
                Instructions = instructions,
                FriendlyName = friendlyName
            }
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        return body.GetProperty("id").GetGuid();
    }
}

// ── MAPS_Resources ────────────────────────────────────────────────────────────

[Trait("Category", "MAPS_Resources")]
public class MapResourceCrudTests : MapResourceTestsBase
{
    public MapResourceCrudTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CreateExternalMapLink_ValidUrl_Returns201()
    {
        var response = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/map-resources/external",
            new
            {
                ExternalUrl = "https://maps.example.com/route-alpha",
                Instructions = (string?)null,
                FriendlyName = "Route Alpha"
            }
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("externalUrl").GetString().Should().Be("https://maps.example.com/route-alpha");
        body.GetProperty("friendlyName").GetString().Should().Be("Route Alpha");
        body.TryGetProperty("id", out _).Should().BeTrue();
    }

    [Fact]
    public async Task CreateExternalMapLink_WithInstructions_Returns201()
    {
        var response = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/map-resources/external",
            new
            {
                ExternalUrl = "https://maps.example.com/route-bravo",
                Instructions = "Use this layer for objective markers.",
                FriendlyName = "Route Bravo"
            }
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("instructions").GetString().Should().Be("Use this layer for objective markers.");
    }

    [Fact]
    public async Task ListMapResources_PlayerRole_Returns200()
    {
        await CreateExternalMapResourceAsync("External Map", "Open in browser");

        var fileResourceId = Guid.NewGuid();
        var uploadResponse = await _commanderClient.GetAsync(
            $"/api/events/{_eventId}/map-resources/{fileResourceId}/upload-url" +
            "?fileName=field-map.pdf&contentType=application/pdf&fileSizeBytes=1024&friendlyName=Field%20Map&instructions=Download%20for%20offline"
        );

        uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var uploadBody = await uploadResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var r2Key = uploadBody.GetProperty("r2Key").GetString();

        var confirmResponse = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/map-resources/{fileResourceId}/confirm",
            new
            {
                R2Key = r2Key,
                ContentType = "application/pdf",
                FileSizeBytes = 1024L
            }
        );
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await _playerClient.GetAsync($"/api/events/{_eventId}/map-resources");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var resources = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement[]>();
        resources.Should().NotBeNull();
        resources!.Length.Should().BeGreaterThanOrEqualTo(2);

        resources.Any(r => r.GetProperty("externalUrl").GetString() is not null).Should().BeTrue();
        resources.Any(r => r.GetProperty("isFile").GetBoolean()).Should().BeTrue();

        foreach (var resource in resources)
        {
            resource.TryGetProperty("r2Key", out _).Should().BeFalse();
            resource.TryGetProperty("downloadUrl", out _).Should().BeFalse();
            resource.TryGetProperty("presignedPutUrl", out _).Should().BeFalse();
        }
    }

    [Fact]
    public async Task DeleteMapResource_CommanderRole_Returns204()
    {
        var resourceId = await CreateExternalMapResourceAsync("Delete Me");

        var deleteResponse = await _commanderClient.DeleteAsync(
            $"/api/events/{_eventId}/map-resources/{resourceId}"
        );
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var secondDelete = await _commanderClient.DeleteAsync(
            $"/api/events/{_eventId}/map-resources/{resourceId}"
        );
        secondDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

// ── MAPS_Files ────────────────────────────────────────────────────────────────

[Trait("Category", "MAPS_Files")]
public class MapFileTests : MapResourceTestsBase
{
    public MapFileTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetMapFileUploadUrl_ValidRequest_Returns200()
    {
        var resourceId = Guid.NewGuid();

        var response = await _commanderClient.GetAsync(
            $"/api/events/{_eventId}/map-resources/{resourceId}/upload-url" +
            "?fileName=terrain.kmz&contentType=application/vnd.google-earth.kmz&fileSizeBytes=2048&friendlyName=Terrain&instructions=Open%20in%20Google%20Earth"
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("uploadId").GetGuid().Should().NotBeEmpty();
        body.GetProperty("presignedPutUrl").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("r2Key").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetMapFileDownloadUrl_PlayerRole_Returns200WithUrl()
    {
        var resourceId = Guid.NewGuid();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.MapResources.Add(new MapResource
            {
                Id = resourceId,
                EventId = _eventId,
                FriendlyName = "Offline Map",
                ContentType = "application/pdf",
                R2Key = $"events/{_eventId}/resources/{resourceId}/files/upload-1/offline-map.pdf",
                Order = 0
            });
            await db.SaveChangesAsync();
        }

        var response = await _playerClient.GetAsync(
            $"/api/events/{_eventId}/map-resources/{resourceId}/download-url"
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("downloadUrl").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetMapFileDownloadUrl_DisallowedMime_Returns400()
    {
        _fileServiceMock
            .Setup(f => f.GenerateUploadUrl(It.IsAny<Guid>(), It.IsAny<Guid>(), "text/plain", It.IsAny<string>()))
            .Throws(new ValidationException("File type 'text/plain' is not permitted."));

        var resourceId = Guid.NewGuid();
        var response = await _commanderClient.GetAsync(
            $"/api/events/{_eventId}/map-resources/{resourceId}/upload-url" +
            "?fileName=notes.txt&contentType=text/plain&fileSizeBytes=512&friendlyName=Notes"
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
