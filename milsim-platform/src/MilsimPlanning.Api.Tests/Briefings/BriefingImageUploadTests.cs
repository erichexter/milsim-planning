using System.Net;
using System.Net.Http.Headers;
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
/// Integration tests for Story 3: POST /api/v1/briefings/{briefingId}/images
/// and GET /api/v1/briefings/{briefingId}/images/{uploadId}.
/// </summary>
public class BriefingImageUploadTestsBase : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    protected readonly PostgreSqlFixture _fixture;
    protected WebApplicationFactory<Program> _factory = null!;
    protected Mock<IEmailService> _emailMock = null!;

    protected HttpClient _briefingAdminClient = null!;
    protected HttpClient _playerClient = null!;
    protected AppUser _briefingAdminUser = null!;

    protected Guid _briefingId;

    public BriefingImageUploadTestsBase(PostgreSqlFixture fixture) => _fixture = fixture;

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
                    }).AddScheme<AuthenticationSchemeOptions, IntegrationTestAuthHandler>(
                        IntegrationTestAuthHandler.SchemeName, _ => { });
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

        // Create briefing_admin user
        _briefingAdminUser = await CreateUserAsync($"img-admin-{Guid.NewGuid():N}@test.com", "briefing_admin");
        _briefingAdminClient = CreateAuthenticatedClient(_briefingAdminUser, "briefing_admin");

        // Create player user (for 403 tests)
        var playerUser = await CreateUserAsync($"img-player-{Guid.NewGuid():N}@test.com", "player");
        _playerClient = CreateAuthenticatedClient(playerUser, "player");

        // Seed a Briefing to upload images to
        _briefingId = Guid.NewGuid();
        var briefing = new Briefing
        {
            Id = _briefingId,
            Title = "Test Briefing for Image Upload",
            ChannelIdentifier = Guid.NewGuid().ToString(),
            PublicationState = "Draft",
            VersionETag = "etag-v1",
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Briefings.Add(briefing);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _briefingAdminClient.Dispose();
        _playerClient.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    protected async Task<AppUser> CreateUserAsync(string email, string role)
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
        var result = await userManager.CreateAsync(user, "TestPass123!");
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

    /// <summary>Builds a multipart form-data request with a small PNG-like payload.</summary>
    protected static MultipartFormDataContent BuildImageContent(
        string fileName = "test.png",
        string contentType = "image/png",
        int sizeBytes = 1024)
    {
        var bytes = new byte[sizeBytes];
        // Write minimal PNG header signature so it looks like a real file
        bytes[0] = 0x89; bytes[1] = 0x50; bytes[2] = 0x4E; bytes[3] = 0x47;

        var byteContent = new ByteArrayContent(bytes);
        byteContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);

        var form = new MultipartFormDataContent();
        form.Add(byteContent, "file", fileName);
        return form;
    }
}

// ── AC-02: POST /api/v1/briefings/{briefingId}/images ──────────────────────

[Trait("Category", "BriefingImageUpload_Post")]
public class BriefingImageUpload_PostTests : BriefingImageUploadTestsBase
{
    public BriefingImageUpload_PostTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task UploadImage_ValidPng_Returns202WithUploadId()
    {
        var form = BuildImageContent("map.png", "image/png");
        var response = await _briefingAdminClient.PostAsync(
            $"/api/v1/briefings/{_briefingId}/images", form);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            because: "AC-02: valid image upload returns 202 Accepted");

        var dto = await response.Content.ReadFromJsonAsync<ImageUploadDto>();
        dto.Should().NotBeNull();
        dto!.UploadId.Should().NotBeEmpty();
        dto.Status.Should().Be("Pending", because: "AC-02: response status is Pending");
        dto.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task UploadImage_ValidJpeg_Returns202()
    {
        var form = BuildImageContent("photo.jpg", "image/jpeg");
        var response = await _briefingAdminClient.PostAsync(
            $"/api/v1/briefings/{_briefingId}/images", form);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            because: "JPEG files are accepted");
    }

    [Fact]
    public async Task UploadImage_ValidWebp_Returns202()
    {
        var form = BuildImageContent("map.webp", "image/webp");
        var response = await _briefingAdminClient.PostAsync(
            $"/api/v1/briefings/{_briefingId}/images", form);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted,
            because: "WebP files are accepted");
    }

    [Fact]
    public async Task UploadImage_InvalidFileType_Returns400()
    {
        // AC: .exe files must be rejected with 400
        var form = BuildImageContent("malware.exe", "application/octet-stream");
        var response = await _briefingAdminClient.PostAsync(
            $"/api/v1/briefings/{_briefingId}/images", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            because: "AC: unsupported file type must return 400");
    }

    [Fact]
    public async Task UploadImage_InvalidFileType_Returns400_ForPdf()
    {
        var form = BuildImageContent("doc.pdf", "application/pdf");
        var response = await _briefingAdminClient.PostAsync(
            $"/api/v1/briefings/{_briefingId}/images", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            because: "PDF files are not valid image types for this endpoint");
    }

    [Fact]
    public async Task UploadImage_PlayerRole_Returns403()
    {
        var form = BuildImageContent("map.png", "image/png");
        var response = await _playerClient.PostAsync(
            $"/api/v1/briefings/{_briefingId}/images", form);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "only BriefingAdmin can upload images");
    }

    [Fact]
    public async Task UploadImage_WithoutAuth_Returns401()
    {
        var anonClient = _factory.CreateClient();
        var form = BuildImageContent("map.png", "image/png");
        var response = await anonClient.PostAsync(
            $"/api/v1/briefings/{_briefingId}/images", form);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "unauthenticated requests must be rejected");
    }

    [Fact]
    public async Task UploadImage_NonExistentBriefing_Returns404()
    {
        var form = BuildImageContent("map.png", "image/png");
        var fakeBriefingId = Guid.NewGuid();
        var response = await _briefingAdminClient.PostAsync(
            $"/api/v1/briefings/{fakeBriefingId}/images", form);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            because: "briefing must exist before uploading images");
    }

    [Fact]
    public async Task UploadImage_CreatesImageUploadRecordInDatabase()
    {
        // AC-04: server creates ImageUpload record with status Pending
        var form = BuildImageContent("db-test.png", "image/png");
        var response = await _briefingAdminClient.PostAsync(
            $"/api/v1/briefings/{_briefingId}/images", form);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var dto = await response.Content.ReadFromJsonAsync<ImageUploadDto>();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var record = await db.ImageUploads.FirstOrDefaultAsync(u => u.Id == dto!.UploadId);
        record.Should().NotBeNull();
        record!.BriefingId.Should().Be(_briefingId);
        record.UploadStatus.Should().Be(UploadStatus.Pending,
            because: "AC-04: ImageUpload created with status=Pending");
        record.OriginalFileName.Should().Be("db-test.png");
    }

    [Fact]
    public async Task UploadImage_CreatesThreeImageResizeJobRecords()
    {
        // AC-04: server creates ImageResizeJob records for 3 variants
        var form = BuildImageContent("resize-test.png", "image/png");
        var response = await _briefingAdminClient.PostAsync(
            $"/api/v1/briefings/{_briefingId}/images", form);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var dto = await response.Content.ReadFromJsonAsync<ImageUploadDto>();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var jobs = await db.ImageResizeJobs
            .Where(j => j.ImageUploadId == dto!.UploadId)
            .OrderBy(j => j.TargetDimensions)
            .ToListAsync();

        jobs.Should().HaveCount(3,
            because: "AC-04: 3 resize variants must be queued: 1280x720, 640x480, 320x240");

        var dimensions = jobs.Select(j => j.TargetDimensions).ToHashSet();
        dimensions.Should().Contain("1280x720");
        dimensions.Should().Contain("640x480");
        dimensions.Should().Contain("320x240");

        jobs.Should().AllSatisfy(j =>
            j.ResizeStatus.Should().Be(ResizeStatus.Queued,
                because: "resize jobs are queued for the background worker (Story 6)"));
    }
}

// ── AC-05: GET /api/v1/briefings/{briefingId}/images/{uploadId} ─────────────

[Trait("Category", "BriefingImageUpload_Get")]
public class BriefingImageUpload_GetTests : BriefingImageUploadTestsBase
{
    public BriefingImageUpload_GetTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetUploadStatus_ExistingUpload_Returns200WithResizeJobs()
    {
        // Upload first
        var form = BuildImageContent("status-test.png", "image/png");
        var postResponse = await _briefingAdminClient.PostAsync(
            $"/api/v1/briefings/{_briefingId}/images", form);
        postResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var uploadDto = await postResponse.Content.ReadFromJsonAsync<ImageUploadDto>();

        // Poll status
        var getResponse = await _briefingAdminClient.GetAsync(
            $"/api/v1/briefings/{_briefingId}/images/{uploadDto!.UploadId}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "AC-05: GET upload status returns 200");

        var statusDto = await getResponse.Content.ReadFromJsonAsync<ImageUploadStatusDto>();
        statusDto.Should().NotBeNull();
        statusDto!.UploadId.Should().Be(uploadDto.UploadId);
        statusDto.UploadStatus.Should().Be("Pending");
        statusDto.ResizeJobs.Should().HaveCount(3,
            because: "polling must return all 3 resize job statuses");

        var jobDimensions = statusDto.ResizeJobs.Select(j => j.Dimensions).ToHashSet();
        jobDimensions.Should().Contain("1280x720");
        jobDimensions.Should().Contain("640x480");
        jobDimensions.Should().Contain("320x240");

        statusDto.ResizeJobs.Should().AllSatisfy(j =>
            j.ResizeStatus.Should().Be("Queued"));
    }

    [Fact]
    public async Task GetUploadStatus_NonExistentUpload_Returns404()
    {
        var fakeUploadId = Guid.NewGuid();
        var response = await _briefingAdminClient.GetAsync(
            $"/api/v1/briefings/{_briefingId}/images/{fakeUploadId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            because: "non-existent upload ID must return 404");
    }

    [Fact]
    public async Task GetUploadStatus_PlayerRole_Returns403()
    {
        var fakeUploadId = Guid.NewGuid();
        var response = await _playerClient.GetAsync(
            $"/api/v1/briefings/{_briefingId}/images/{fakeUploadId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "only BriefingAdmin can query upload status");
    }
}
