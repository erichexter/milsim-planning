namespace MilsimPlanning.Api.Models.Notifications;

public record QueueRosterDecisionRequest(
    Guid EventPlayerId,
    string Decision,
    string RequestedChangeSummary,
    string? CommanderNote
);
