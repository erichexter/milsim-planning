namespace MilsimPlanning.Api.Models.Events;

/// <summary>
/// Request to update a member's role within an event (e.g., assign FactionCommander).
/// Only EventOwner can invoke this operation.
/// </summary>
public record UpdateEventMemberRoleRequest(
    string NewRole,         // Role name: "faction_commander", "squad_leader", etc.
    Guid? FactionId = null  // Optional: faction assignment for FactionCommander role
);
