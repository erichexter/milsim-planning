using FluentValidation;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
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
using System.Net.Http.Json;
using Xunit;

namespace MilsimPlanning.Api.Tests.Content;

/// <summary>
/// Integration tests for Info Section API (CONT-01..05).
/// Requires Docker Desktop for Testcontainers PostgreSQL.
/// </summary>
public class ContentTestsBase : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    protected readonly PostgreSqlFixture _fixture;
    protected WebApplicationFactory<Program> _factory = null!;
    protected Mock<IEmailService> _emailMock = null!;
    protected Mock<IFileService> _fileServiceMock = null!;
    protected HttpClient _commanderClient = null!;
    protected HttpClient _playerClient = null!;
    protected Guid _eventId;

    public ContentTestsBase(PostgreSqlFixture fixture)
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
            .Returns(new UploadUrlResponse(Guid.NewGuid(), "https://fake.r2.com/presigned", "events/test/key"));
        _fileServiceMock
            .Setup(f => f.GenerateDownloadUrl(It.IsAny<string>()))
            .Returns("https://fake.r2.com/download");

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = IntegrationTestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = IntegrationTestAuthHandler.SchemeName;
                    }).AddScheme<AuthenticationSchemeOptions, IntegrationTestAuthHandler>(IntegrationTestAuthHandler.SchemeName, _ => { });

                    services.RemoveAll<DbContextOptions<AppDbContext>>();
                    services.RemoveAll<AppDbContext>();
                    services.AddDbContext<AppDbContext>(opts =>
                        opts.UseNpgsql(_fixture.ConnectionString));

                    services.RemoveAll<IEmailService>();
                    services.AddSingleton(_emailMock.Object);

                    // Mock IFileService so tests don't need real R2 credentials
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

        var commanderEmail = $"cont-cmdr-{Guid.NewGuid():N}@test.com";
        var commander = new AppUser { UserName = commanderEmail, Email = commanderEmail, EmailConfirmed = true };
        await userManager.CreateAsync(commander, "TestPass123!");
        await userManager.AddToRoleAsync(commander, "faction_commander");
        commander.Profile = new UserProfile { UserId = commander.Id, Callsign = "Commander", DisplayName = "Commander", User = commander };

        var playerEmail = $"cont-player-{Guid.NewGuid():N}@test.com";
        var player = new AppUser { UserName = playerEmail, Email = playerEmail, EmailConfirmed = true };
        await userManager.CreateAsync(player, "TestPass123!");
        await userManager.AddToRoleAsync(player, "player");
        player.Profile = new UserProfile { UserId = player.Id, Callsign = "Player1", DisplayName = "Player1", User = player };

        _eventId = Guid.NewGuid();
        var factionId = Guid.NewGuid();
        var faction = new Faction { Id = factionId, Name = "Test Faction", CommanderId = commander.Id, EventId = _eventId };
        var testEvent = new Event { Id = _eventId, Name = "Content Test Event", Status = EventStatus.Draft, FactionId = factionId, Faction = faction };

        db.Events.Add(testEvent);
        db.EventMemberships.Add(new EventMembership { UserId = commander.Id, EventId = _eventId, Role = "faction_commander" });
        db.EventMemberships.Add(new EventMembership { UserId = player.Id, EventId = _eventId, Role = "player" });
        await db.SaveChangesAsync();

        _commanderClient = _factory.CreateClient();
        _commanderClient.DefaultRequestHeaders.Add(IntegrationTestAuthHandler.UserIdHeader, commander.Id);
        _commanderClient.DefaultRequestHeaders.Add(IntegrationTestAuthHandler.RoleHeader, "faction_commander");

        _playerClient = _factory.CreateClient();
        _playerClient.DefaultRequestHeaders.Add(IntegrationTestAuthHandler.UserIdHeader, player.Id);
        _playerClient.DefaultRequestHeaders.Add(IntegrationTestAuthHandler.RoleHeader, "player");
    }

    public Task DisposeAsync()
    {
        _commanderClient.Dispose();
        _playerClient.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>Helper: create an info section and return its ID.</summary>
    protected async Task<Guid> CreateSectionAsync(string title = "Test Section", string? body = null)
    {
        var response = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/info-sections",
            new { Title = title, BodyMarkdown = body }
        );
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var content = await response.Content.ReadFromJsonAsync<dynamic>();
        return Guid.Parse(content!.GetProperty("id").GetString()!);
    }
}

// ── CONT_Sections ─────────────────────────────────────────────────────────────

[Trait("Category", "CONT_Sections")]
public class InfoSectionCrudTests : ContentTestsBase
{
    public InfoSectionCrudTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task CreateInfoSection_ValidRequest_Returns201()
    {
        var response = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/info-sections",
            new { Title = "Rules of Engagement", BodyMarkdown = "## ROE\n\nNo blue-on-blue." }
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("title").GetString().Should().Be("Rules of Engagement");
        body.GetProperty("order").GetInt32().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task CreateInfoSection_EmptyTitle_Returns400()
    {
        var response = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/info-sections",
            new { Title = "", BodyMarkdown = (string?)null }
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetInfoSections_CommanderRole_Returns200()
    {
        // Create a section first
        await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/info-sections",
            new { Title = "Intel Brief", BodyMarkdown = "Sector overview." }
        );

        var response = await _commanderClient.GetAsync($"/api/events/{_eventId}/info-sections");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var sections = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement[]>();
        sections.Should().NotBeNull();
        sections!.Length.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task UpdateInfoSection_ValidRequest_Returns204()
    {
        var sectionId = await CreateSectionAsync("Original Title");

        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/events/{_eventId}/info-sections/{sectionId}",
            new { Title = "Updated Title", BodyMarkdown = "Updated body." }
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify change persisted
        var sections = await _commanderClient.GetFromJsonAsync<System.Text.Json.JsonElement[]>($"/api/events/{_eventId}/info-sections");
        var updated = sections!.FirstOrDefault(s => s.GetProperty("id").GetString() == sectionId.ToString());
        updated.GetProperty("title").GetString().Should().Be("Updated Title");
    }

    [Fact]
    public async Task DeleteInfoSection_CommanderRole_Returns204()
    {
        var sectionId = await CreateSectionAsync("Section to Delete");

        var response = await _commanderClient.DeleteAsync(
            $"/api/events/{_eventId}/info-sections/{sectionId}"
        );

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it's gone (second delete returns 404)
        var secondDelete = await _commanderClient.DeleteAsync(
            $"/api/events/{_eventId}/info-sections/{sectionId}"
        );
        secondDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

// ── CONT_Attachments ──────────────────────────────────────────────────────────

[Trait("Category", "CONT_Attachments")]
public class InfoSectionAttachmentTests : ContentTestsBase
{
    public InfoSectionAttachmentTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetUploadUrl_ValidRequest_Returns200WithPresignedUrl()
    {
        var sectionId = await CreateSectionAsync("Section for Upload");

        var response = await _commanderClient.GetAsync(
            $"/api/events/{_eventId}/info-sections/{sectionId}/attachments/upload-url" +
            $"?fileName=comms-plan.pdf&contentType=application/pdf&fileSizeBytes=1048576"
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("presignedPutUrl").GetString().Should().NotBeNullOrEmpty();
        body.GetProperty("r2Key").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetUploadUrl_FileSizeOver10MB_Returns400()
    {
        var sectionId = await CreateSectionAsync("Section Over Limit");

        var oversizeBytes = 10L * 1024 * 1024 + 1; // 10 MB + 1 byte
        var response = await _commanderClient.GetAsync(
            $"/api/events/{_eventId}/info-sections/{sectionId}/attachments/upload-url" +
            $"?fileName=large.pdf&contentType=application/pdf&fileSizeBytes={oversizeBytes}"
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetUploadUrl_DisallowedMimeType_Returns400()
    {
        _fileServiceMock
            .Setup(f => f.GenerateUploadUrl(It.IsAny<Guid>(), It.IsAny<Guid>(), "application/x-msdownload", It.IsAny<string>()))
            .Throws(new ValidationException("File type 'application/x-msdownload' is not permitted."));

        var sectionId = await CreateSectionAsync("Section Bad MIME");

        var response = await _commanderClient.GetAsync(
            $"/api/events/{_eventId}/info-sections/{sectionId}/attachments/upload-url" +
            $"?fileName=script.exe&contentType=application/x-msdownload&fileSizeBytes=1024"
        );

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ConfirmAttachment_ValidUploadId_Returns201()
    {
        var sectionId = await CreateSectionAsync("Section for Confirm");

        var response = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/info-sections/{sectionId}/attachments/confirm",
            new
            {
                R2Key = "events/test-event/resources/test-resource/files/upload-123/doc.pdf",
                FriendlyName = "Operations Order",
                ContentType = "application/pdf",
                FileSizeBytes = 204800L
            }
        );

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("friendlyName").GetString().Should().Be("Operations Order");
    }
}

// ── CONT_Reorder ──────────────────────────────────────────────────────────────

[Trait("Category", "CONT_Reorder")]
public class InfoSectionReorderTests : ContentTestsBase
{
    public InfoSectionReorderTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ReorderSections_ValidOrderedIds_Returns204AndPersists()
    {
        // Create 3 sections (they'll have order 0, 1, 2)
        var id1 = await CreateSectionAsync("Alpha Section");
        var id2 = await CreateSectionAsync("Bravo Section");
        var id3 = await CreateSectionAsync("Charlie Section");

        // Reorder: reverse order [id3, id2, id1]
        var reorderResponse = await _commanderClient.PatchAsJsonAsync(
            $"/api/events/{_eventId}/info-sections/reorder",
            new { OrderedIds = new[] { id3, id2, id1 } }
        );

        reorderResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify via GET that order is now reversed
        var sections = await _commanderClient.GetFromJsonAsync<System.Text.Json.JsonElement[]>(
            $"/api/events/{_eventId}/info-sections"
        );

        sections.Should().NotBeNull();

        // Find the sections by ID and assert their new order values
        var section1 = sections!.First(s => s.GetProperty("id").GetString() == id1.ToString());
        var section2 = sections.First(s => s.GetProperty("id").GetString() == id2.ToString());
        var section3 = sections.First(s => s.GetProperty("id").GetString() == id3.ToString());

        // After reorder [id3, id2, id1]: id3 should have order 0, id2 order 1, id1 order 2
        section3.GetProperty("order").GetInt32().Should().Be(0);
        section2.GetProperty("order").GetInt32().Should().Be(1);
        section1.GetProperty("order").GetInt32().Should().Be(2);

        // GET returns them sorted by order — id3 should be first
        sections[0].GetProperty("id").GetString().Should().Be(id3.ToString());
    }
}
