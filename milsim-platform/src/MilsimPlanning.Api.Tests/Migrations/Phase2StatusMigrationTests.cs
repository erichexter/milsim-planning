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
using System.Data;
using Xunit;

namespace MilsimPlanning.Api.Tests.Migrations;

[Trait("Category", "MIGR_Phase2")]
public class Phase2StatusMigrationTests : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private WebApplicationFactory<Program> _factory = null!;

    public Phase2StatusMigrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        var emailMock = new Mock<IEmailService>();
        emailMock.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var fileServiceMock = new Mock<IFileService>();
        fileServiceMock
            .Setup(f => f.GenerateUploadUrl(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new UploadUrlResponse(Guid.NewGuid(), "https://fake.r2.com/upload", "events/test/key"));
        fileServiceMock
            .Setup(f => f.GenerateDownloadUrl(It.IsAny<string>()))
            .Returns("https://fake.r2.com/download");

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<AppDbContext>>();
                    services.RemoveAll<AppDbContext>();
                    services.AddDbContext<AppDbContext>(opts => opts.UseNpgsql(_fixture.ConnectionString));

                    services.RemoveAll<IEmailService>();
                    services.AddSingleton(emailMock.Object);

                    services.RemoveAll<IFileService>();
                    services.AddScoped<IFileService>(_ => fileServiceMock.Object);
                });
            });

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task MigrateAsync_FromInitialToPhase3_ConvertsEventStatusToIntegerAndPreservesEnumSemantics()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roleManager.RoleExistsAsync("faction_commander"))
        {
            await roleManager.CreateAsync(new IdentityRole("faction_commander"));
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var commanderEmail = $"migr-cmdr-{Guid.NewGuid():N}@test.com";
        var commander = new AppUser { UserName = commanderEmail, Email = commanderEmail, EmailConfirmed = true };
        await userManager.CreateAsync(commander, "TestPass123!");
        await userManager.AddToRoleAsync(commander, "faction_commander");

        var factionId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var faction = new Faction
        {
            Id = factionId,
            EventId = eventId,
            CommanderId = commander.Id,
            Name = "Migration Faction"
        };
        var testEvent = new Event
        {
            Id = eventId,
            Name = "Migration Test Event",
            Status = EventStatus.Draft,
            FactionId = factionId,
            Faction = faction
        };

        db.Events.Add(testEvent);
        await db.SaveChangesAsync();

        var reloaded = await db.Events.AsNoTracking().SingleAsync(e => e.Id == eventId);
        reloaded.Status.Should().Be(EventStatus.Draft);

        var columnType = await GetStatusColumnTypeAsync(db);
        columnType.Should().Be("integer");

        var persistedValue = await GetStoredStatusValueAsync(db, eventId);
        persistedValue.Should().Be((int)EventStatus.Draft);
    }

    private static async Task<string> GetStatusColumnTypeAsync(AppDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT data_type
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = 'Events' AND column_name = 'Status';
            """;

        var result = await command.ExecuteScalarAsync();
        return result?.ToString() ?? string.Empty;
    }

    private static async Task<int> GetStoredStatusValueAsync(AppDbContext db, Guid eventId)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT \"Status\" FROM \"Events\" WHERE \"Id\" = @id;";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@id";
        parameter.Value = eventId;
        command.Parameters.Add(parameter);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }
}
