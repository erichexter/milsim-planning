using Microsoft.EntityFrameworkCore;
using MilsimPlanning.Api.Data;
using Resend;

namespace MilsimPlanning.Api.Infrastructure.BackgroundJobs;

public class NotificationWorker : BackgroundService
{
    private readonly INotificationQueue _queue;
    private readonly IServiceProvider _services;
    private readonly ILogger<NotificationWorker> _logger;

    public NotificationWorker(
        INotificationQueue queue,
        IServiceProvider services,
        ILogger<NotificationWorker> logger)
    {
        _queue = queue;
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = _services.CreateAsyncScope();
                var resend = scope.ServiceProvider.GetRequiredService<IResend>();
                var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

                switch (job)
                {
                    case BlastNotificationJob blastJob:
                        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        await ProcessBlastAsync(blastJob, resend, db, config, stoppingToken);
                        break;
                    case SquadChangeJob squadChangeJob:
                        await ProcessSquadChangeAsync(squadChangeJob, resend, config, stoppingToken);
                        break;
                    case RosterChangeDecisionJob rosterDecisionJob:
                        await ProcessRosterChangeDecisionAsync(rosterDecisionJob, resend, config, stoppingToken);
                        break;
                    default:
                        _logger.LogWarning("Unknown notification job type {JobType}", job.GetType().Name);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process notification job");
            }
        }
    }

    internal static string BuildBlastHtml(string subject, string body)
    {
        var safeBody = body.Replace("\r\n", "\n").Replace("\n", "<br>");
        return $"<h1>{subject}</h1><p>{safeBody}</p>";
    }

    internal static string BuildSquadChangeHtml(SquadChangeJob job)
    {
        return $"<h1>Squad Assignment Updated</h1>" +
               $"<p>Hello {job.RecipientName},</p>" +
               $"<p>Your assignment has changed.</p>" +
               $"<p><strong>From:</strong> {job.OldSquadName}, {job.OldPlatoonName}</p>" +
               $"<p><strong>To:</strong> {job.NewSquadName}, {job.NewPlatoonName}</p>";
    }

    internal static string BuildRosterDecisionHtml(RosterChangeDecisionJob job)
    {
        var isApproved = string.Equals(job.Decision, "approved", StringComparison.OrdinalIgnoreCase);
        var outcomeText = isApproved ? "approved" : "denied";
        var actionText = isApproved
            ? "Your roster change request has been approved."
            : "Your roster change request has been denied.";
        var commanderNote = string.IsNullOrWhiteSpace(job.CommanderNote)
            ? string.Empty
            : $"<p><strong>Commander note:</strong> {job.CommanderNote}</p>";

        return $"<h1>Roster Change Decision</h1>" +
               $"<p>Hello {job.RecipientName},</p>" +
               $"<p>{actionText}</p>" +
               $"<p><strong>Event:</strong> {job.EventName}</p>" +
               $"<p><strong>Decision:</strong> {outcomeText}</p>" +
               $"<p><strong>Requested change:</strong> {job.RequestedChangeSummary}</p>" +
               commanderNote;
    }

    private static async Task ProcessBlastAsync(
        BlastNotificationJob job,
        IResend resend,
        AppDbContext db,
        IConfiguration config,
        CancellationToken ct)
    {
        var fromAddress = config["Resend:FromAddress"] ?? "noreply@yourdomain.com";
        var html = BuildBlastHtml(job.Subject, job.Body);

        foreach (var chunk in job.RecipientEmails.Chunk(100))
        {
            var messages = chunk
                .Select(email =>
                {
                    var message = new EmailMessage
                    {
                        From = fromAddress,
                        Subject = job.Subject,
                        HtmlBody = html
                    };
                    message.To.Add(email);
                    return message;
                })
                .ToList();

            if (messages.Count > 0)
            {
                await resend.EmailBatchAsync(messages, ct);
                await Task.Delay(200, ct);
            }
        }

        var blast = await db.NotificationBlasts.FirstOrDefaultAsync(b => b.Id == job.NotificationBlastId, ct);
        if (blast is not null)
        {
            blast.RecipientCount = job.RecipientEmails.Count;
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task ProcessSquadChangeAsync(
        SquadChangeJob job,
        IResend resend,
        IConfiguration config,
        CancellationToken ct)
    {
        var fromAddress = config["Resend:FromAddress"] ?? "noreply@yourdomain.com";
        var html = BuildSquadChangeHtml(job);

        var message = new EmailMessage
        {
            From = fromAddress,
            Subject = "Your squad assignment has changed",
            HtmlBody = html
        };
        message.To.Add(job.RecipientEmail);

        await resend.EmailSendAsync(message, ct);
    }

    private static async Task ProcessRosterChangeDecisionAsync(
        RosterChangeDecisionJob job,
        IResend resend,
        IConfiguration config,
        CancellationToken ct)
    {
        var fromAddress = config["Resend:FromAddress"] ?? "noreply@yourdomain.com";
        var isApproved = string.Equals(job.Decision, "approved", StringComparison.OrdinalIgnoreCase);

        var message = new EmailMessage
        {
            From = fromAddress,
            Subject = $"Roster change request { (isApproved ? "approved" : "denied") }",
            HtmlBody = BuildRosterDecisionHtml(job)
        };
        message.To.Add(job.RecipientEmail);

        await resend.EmailSendAsync(message, ct);
    }
}
