namespace MilsimPlanning.Api.Models.Events;

/// <summary>Request to assign a user as Faction Commander within an event.</summary>
public record AssignFactionCommanderRequest(
    string UserId,              // User ID (UUID string) to promote
    Guid FactionId              // Faction ID where user becomes commander
);
