using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Infrastructure.BackgroundJobs;
using MilsimPlanning.Api.Services;
using MilsimPlanning.Api.Tests.Fixtures;
using Moq;
using Resend;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Xunit;

namespace MilsimPlanning.Api.Tests.Notifications;

public class NotificationTestsBase : IClassFixture<PostgreSqlFixture>, IAsyncLifetime
{
    protected readonly PostgreSqlFixture _fixture;
    protected WebApplicationFactory<Program> _factory = null!;
    protected Mock<IEmailService> _emailMock = null!;
    protected Mock<INotificationQueue> _queueMock = null!;
    protected HttpClient _commanderClient = null!;
    protected HttpClient _playerClient = null!;
    protected Guid _eventId;
    protected string _commanderUserId = string.Empty;
    protected string _playerUserId = string.Empty;

    public NotificationTestsBase(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _emailMock = new Mock<IEmailService>();
        _emailMock.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                  .Returns(Task.CompletedTask);

        _queueMock = new Mock<INotificationQueue>();
        _queueMock.Setup(q => q.EnqueueAsync(It.IsAny<NotificationJob>(), It.IsAny<CancellationToken>()))
                  .Returns(ValueTask.CompletedTask);
        _queueMock.Setup(q => q.ReadAllAsync(It.IsAny<CancellationToken>()))
                  .Returns((CancellationToken ct) => EmptyQueueAsync(ct));

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
                    services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

                    services.RemoveAll<DbContextOptions<AppDbContext>>();
                    services.RemoveAll<AppDbContext>();
                    services.AddDbContext<AppDbContext>(opts =>
                        opts.UseNpgsql(_fixture.ConnectionString));

                    services.RemoveAll<IEmailService>();
                    services.AddSingleton(_emailMock.Object);

                    services.RemoveAll<INotificationQueue>();
                    services.AddSingleton(_queueMock.Object);

                    var notificationWorkerDescriptors = services
                        .Where(d => d.ServiceType == typeof(IHostedService)
                                    && d.ImplementationType == typeof(NotificationWorker))
                        .ToList();

                    foreach (var descriptor in notificationWorkerDescriptors)
                    {
                        services.Remove(descriptor);
                    }
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

        var commanderEmail = $"notf-cmdr-{Guid.NewGuid():N}@test.com";
        var commander = new AppUser { UserName = commanderEmail, Email = commanderEmail, EmailConfirmed = true };
        await userManager.CreateAsync(commander, "TestPass123!");
        await userManager.AddToRoleAsync(commander, "faction_commander");
        commander.Profile = new UserProfile { UserId = commander.Id, Callsign = "Commander", DisplayName = "Commander", User = commander };
        _commanderUserId = commander.Id;

        var playerEmail = $"notf-player-{Guid.NewGuid():N}@test.com";
        var player = new AppUser { UserName = playerEmail, Email = playerEmail, EmailConfirmed = true };
        await userManager.CreateAsync(player, "TestPass123!");
        await userManager.AddToRoleAsync(player, "player");
        player.Profile = new UserProfile { UserId = player.Id, Callsign = "Player1", DisplayName = "Player1", User = player };
        _playerUserId = player.Id;

        _eventId = Guid.NewGuid();
        var factionId = Guid.NewGuid();
        var faction = new Faction { Id = factionId, Name = "Test Faction", CommanderId = commander.Id, EventId = _eventId };
        var testEvent = new Event { Id = _eventId, Name = "Notification Test Event", Status = EventStatus.Draft, FactionId = factionId, Faction = faction };

        db.Events.Add(testEvent);
        db.EventMemberships.Add(new EventMembership { UserId = commander.Id, EventId = _eventId, Role = "faction_commander" });
        db.EventMemberships.Add(new EventMembership { UserId = player.Id, EventId = _eventId, Role = "player" });
        await db.SaveChangesAsync();

        _commanderClient = _factory.CreateClient();
        _commanderClient.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, _commanderUserId);
        _commanderClient.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "faction_commander");

        _playerClient = _factory.CreateClient();
        _playerClient.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, _playerUserId);
        _playerClient.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "player");
    }

    public Task DisposeAsync()
    {
        _commanderClient.Dispose();
        _playerClient.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    protected AppDbContext GetDb()
    {
        var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDbContext>();
    }

    protected async Task<Guid> SeedEventPlayerAsync(string email, string name, string? userId = null, Guid? squadId = null)
    {
        using var db = GetDb();

        Guid? platoonId = null;
        if (squadId.HasValue)
        {
            var squad = await db.Squads.FirstAsync(s => s.Id == squadId.Value);
            platoonId = squad.PlatoonId;
        }

        var player = new EventPlayer
        {
            EventId = _eventId,
            Email = email,
            Name = name,
            UserId = userId,
            SquadId = squadId,
            PlatoonId = platoonId
        };

        db.EventPlayers.Add(player);
        await db.SaveChangesAsync();
        return player.Id;
    }

    protected async Task<Guid> SeedSquadAsync(string platoonName, string squadName)
    {
        using var db = GetDb();
        var faction = await db.Factions.FirstAsync(f => f.EventId == _eventId);

        var platoon = new Platoon
        {
            FactionId = faction.Id,
            Name = platoonName,
            Order = await db.Platoons.Where(p => p.FactionId == faction.Id).CountAsync() + 1
        };
        db.Platoons.Add(platoon);
        await db.SaveChangesAsync();

        var squad = new Squad
        {
            PlatoonId = platoon.Id,
            Name = squadName,
            Order = 1
        };
        db.Squads.Add(squad);
        await db.SaveChangesAsync();

        return squad.Id;
    }

    private static async IAsyncEnumerable<NotificationJob> EmptyQueueAsync([EnumeratorCancellation] CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(50, ct);
        }

        yield break;
    }
}

internal sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";
    public const string UserIdHeader = "X-Test-UserId";
    public const string RoleHeader = "X-Test-Role";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userId = Request.Headers[UserIdHeader].FirstOrDefault();
        var role = Request.Headers[RoleHeader].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(role))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing test auth headers."));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim("sub", userId),
            new Claim(ClaimTypes.Role, role)
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

[Trait("Category", "NOTF_Blast")]
public class NotificationBlastTests : NotificationTestsBase
{
    public NotificationBlastTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task SendBlast_ValidRequest_Returns202()
    {
        await SeedEventPlayerAsync($"blast-{Guid.NewGuid():N}@test.com", "Blast Player", _playerUserId);

        var response = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/notification-blasts",
            new { subject = "Mission Update", body = "Report to briefing room" });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        payload.GetProperty("blastId").GetString().Should().NotBeNullOrWhiteSpace();
        payload.GetProperty("recipientCount").GetInt32().Should().Be(1);

        _queueMock.Verify(q => q.EnqueueAsync(
            It.IsAny<BlastNotificationJob>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendBlast_CreatesNotificationBlastRecord()
    {
        await SeedEventPlayerAsync($"db-{Guid.NewGuid():N}@test.com", "DB Player", _playerUserId);

        var response = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/notification-blasts",
            new { subject = "Subject Persist", body = "Persistent body" });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var blastId = Guid.Parse((await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("blastId").GetString()!);

        using var db = GetDb();
        var blast = await db.NotificationBlasts.FirstOrDefaultAsync(b => b.Id == blastId);
        blast.Should().NotBeNull();
        blast!.Subject.Should().Be("Subject Persist");
        blast.Body.Should().Be("Persistent body");
        blast.EventId.Should().Be(_eventId);
    }

    [Fact]
    public async Task GetBlastLog_ReturnsChronologicalList()
    {
        using (var db = GetDb())
        {
            db.NotificationBlasts.Add(new NotificationBlast
            {
                Id = Guid.NewGuid(),
                EventId = _eventId,
                Subject = "Older",
                Body = "Old body",
                SentAt = DateTime.UtcNow.AddMinutes(-5),
                RecipientCount = 1
            });
            db.NotificationBlasts.Add(new NotificationBlast
            {
                Id = Guid.NewGuid(),
                EventId = _eventId,
                Subject = "Newer",
                Body = "New body",
                SentAt = DateTime.UtcNow,
                RecipientCount = 2
            });
            await db.SaveChangesAsync();
        }

        var response = await _playerClient.GetAsync($"/api/events/{_eventId}/notification-blasts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        list.Should().NotBeNull();
        list!.Count.Should().BeGreaterThanOrEqualTo(2);
        list[0].GetProperty("subject").GetString().Should().Be("Newer");
        list[1].GetProperty("subject").GetString().Should().Be("Older");
    }
}

[Trait("Category", "NOTF_Squad")]
public class SquadChangeNotificationTests : NotificationTestsBase
{
    public SquadChangeNotificationTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task AssignSquad_PlayerWithAccount_EnqueuesSquadChangeJob()
    {
        var oldSquadId = await SeedSquadAsync("Old Platoon", "Old Squad");
        var newSquadId = await SeedSquadAsync("New Platoon", "New Squad");
        var playerId = await SeedEventPlayerAsync(
            $"account-{Guid.NewGuid():N}@test.com",
            "Account Player",
            _playerUserId,
            oldSquadId);

        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/event-players/{playerId}/squad",
            new { squadId = newSquadId });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        _queueMock.Verify(q => q.EnqueueAsync(
            It.Is<SquadChangeJob>(job =>
                job.RecipientName == "Account Player" &&
                job.OldPlatoonName == "Old Platoon" &&
                job.OldSquadName == "Old Squad" &&
                job.NewPlatoonName == "New Platoon" &&
                job.NewSquadName == "New Squad"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssignSquad_PlayerWithoutAccount_DoesNotEnqueueJob()
    {
        var oldSquadId = await SeedSquadAsync("Old Platoon", "Old Squad");
        var newSquadId = await SeedSquadAsync("New Platoon", "New Squad");
        var playerId = await SeedEventPlayerAsync(
            $"no-account-{Guid.NewGuid():N}@test.com",
            "No Account Player",
            null,
            oldSquadId);

        var response = await _commanderClient.PutAsJsonAsync(
            $"/api/event-players/{playerId}/squad",
            new { squadId = newSquadId });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        _queueMock.Verify(q => q.EnqueueAsync(
            It.IsAny<SquadChangeJob>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}

[Trait("Category", "NOTF_Decision_Worker")]
public class RosterDecisionNotificationWorkerTests
{
    [Fact]
    public async Task RosterDecisionJob_Approved_SendsEmail()
    {
        var resendMock = new Mock<IResend>();
        resendMock
            .Setup(r => r.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResendResponse<Guid>)null!);

        var queue = new SingleJobNotificationQueue(new RosterChangeDecisionJob(
            RecipientEmail: "player@test.com",
            RecipientName: "Alpha One",
            EventName: "Operation Dawn",
            Decision: "approved",
            RequestedChangeSummary: "Move to recon element",
            CommanderNote: "Approved after platoon review"));

        var worker = BuildWorker(queue, resendMock.Object);

        await RunUntilCanceledAsync(worker);

        resendMock.Verify(r => r.EmailSendAsync(
                It.Is<EmailMessage>(message =>
                    message.Subject.Contains("approved", StringComparison.OrdinalIgnoreCase) &&
                    message.HtmlBody != null &&
                    message.HtmlBody.Contains("Alpha One") &&
                    message.HtmlBody.Contains("approved", StringComparison.OrdinalIgnoreCase) &&
                    message.HtmlBody.Contains("Move to recon element") &&
                    message.HtmlBody.Contains("Commander note", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RosterDecisionJob_Denied_SendsEmail()
    {
        var resendMock = new Mock<IResend>();
        resendMock
            .Setup(r => r.EmailSendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResendResponse<Guid>)null!);

        var queue = new SingleJobNotificationQueue(new RosterChangeDecisionJob(
            RecipientEmail: "player@test.com",
            RecipientName: "Bravo Two",
            EventName: "Operation Dusk",
            Decision: "denied",
            RequestedChangeSummary: "Switch squads",
            CommanderNote: "Denied due to role imbalance"));

        var worker = BuildWorker(queue, resendMock.Object);

        await RunUntilCanceledAsync(worker);

        resendMock.Verify(r => r.EmailSendAsync(
                It.Is<EmailMessage>(message =>
                    message.Subject.Contains("denied", StringComparison.OrdinalIgnoreCase) &&
                    message.HtmlBody != null &&
                    message.HtmlBody.Contains("Bravo Two") &&
                    message.HtmlBody.Contains("denied", StringComparison.OrdinalIgnoreCase) &&
                    message.HtmlBody.Contains("Switch squads") &&
                    message.HtmlBody.Contains("Commander note", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static TestableNotificationWorker BuildWorker(INotificationQueue queue, IResend resend)
    {
        var services = new ServiceCollection();
        services.AddSingleton(resend);
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Resend:FromAddress"] = "noreply@test.com"
        }).Build());
        var provider = services.BuildServiceProvider();

        return new TestableNotificationWorker(queue, provider, Mock.Of<ILogger<NotificationWorker>>());
    }

    private static async Task RunUntilCanceledAsync(TestableNotificationWorker worker)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        try
        {
            await worker.RunAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // expected - queue waits for additional items after first job
        }
    }

    private sealed class TestableNotificationWorker : NotificationWorker
    {
        public TestableNotificationWorker(INotificationQueue queue, IServiceProvider services, ILogger<NotificationWorker> logger)
            : base(queue, services, logger)
        {
        }

        public Task RunAsync(CancellationToken ct) => ExecuteAsync(ct);
    }

    private sealed class SingleJobNotificationQueue : INotificationQueue
    {
        private readonly NotificationJob _job;

        public SingleJobNotificationQueue(NotificationJob job)
        {
            _job = job;
        }

        public ValueTask EnqueueAsync(NotificationJob job, CancellationToken ct = default)
            => ValueTask.CompletedTask;

        public async IAsyncEnumerable<NotificationJob> ReadAllAsync([EnumeratorCancellation] CancellationToken ct)
        {
            yield return _job;
            await Task.Delay(Timeout.Infinite, ct);
        }
    }
}

[Trait("Category", "NOTF_Decision")]
public class RosterDecisionQueueApiTests : NotificationTestsBase
{
    public RosterDecisionQueueApiTests(PostgreSqlFixture fixture) : base(fixture) { }

    [Fact]
    public async Task QueueRosterDecision_Approved_Returns202_AndEnqueuesJob()
    {
        var eventPlayerId = await SeedEventPlayerAsync(
            $"decision-approve-{Guid.NewGuid():N}@test.com",
            "Decision Player",
            _playerUserId);

        var response = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/roster-change-decisions",
            new
            {
                eventPlayerId,
                decision = "approved",
                requestedChangeSummary = "Move to recon",
                commanderNote = "Approved"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        payload.GetProperty("queued").GetBoolean().Should().BeTrue();

        _queueMock.Verify(q => q.EnqueueAsync(
            It.Is<RosterChangeDecisionJob>(job =>
                job.Decision == "approved" &&
                job.RecipientName == "Decision Player" &&
                job.RequestedChangeSummary == "Move to recon"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueueRosterDecision_Denied_Returns202_AndEnqueuesJob()
    {
        var eventPlayerId = await SeedEventPlayerAsync(
            $"decision-deny-{Guid.NewGuid():N}@test.com",
            "Denied Player",
            _playerUserId);

        var response = await _commanderClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/roster-change-decisions",
            new
            {
                eventPlayerId,
                decision = "denied",
                requestedChangeSummary = "Stay current assignment"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        _queueMock.Verify(q => q.EnqueueAsync(
            It.Is<RosterChangeDecisionJob>(job =>
                job.Decision == "denied" &&
                job.RecipientName == "Denied Player" &&
                job.RequestedChangeSummary == "Stay current assignment"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueueRosterDecision_PlayerRole_Forbidden()
    {
        var eventPlayerId = await SeedEventPlayerAsync(
            $"decision-player-{Guid.NewGuid():N}@test.com",
            "Forbidden Player",
            _playerUserId);

        var response = await _playerClient.PostAsJsonAsync(
            $"/api/events/{_eventId}/roster-change-decisions",
            new
            {
                eventPlayerId,
                decision = "approved",
                requestedChangeSummary = "Move to support"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        _queueMock.Verify(q => q.EnqueueAsync(
            It.IsAny<RosterChangeDecisionJob>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
