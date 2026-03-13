namespace MilsimPlanning.Api.Infrastructure.BackgroundJobs;

public abstract record NotificationJob;

public record BlastNotificationJob(
    Guid EventId,
    Guid NotificationBlastId,
    string Subject,
    string Body,
    List<string> RecipientEmails
) : NotificationJob;

public record SquadChangeJob(
    string RecipientEmail,
    string RecipientName,
    string OldPlatoonName,
    string OldSquadName,
    string NewPlatoonName,
    string NewSquadName
) : NotificationJob;

// RosterChangeDecisionJob added in Phase 4 (RCHG)
