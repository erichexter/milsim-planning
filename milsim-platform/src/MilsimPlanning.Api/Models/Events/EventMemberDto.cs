namespace MilsimPlanning.Api.Models.Events;

/// <summary>
/// DTO for a single event member (EventMembership with user details).
/// Used in event roster and member assignment flows.
/// </summary>
public record EventMemberDto(
    Guid MemberId,
    string UserId,
    string UserEmail,
    string Role,                // Role name: "player", "squad_leader", "platoon_leader", "faction_commander", "event_owner"
    string RoleLabel,           // Display name: "Player", "Squad Leader", etc.
    Guid? FactionId,            // null for EventOwner, assigned faction for FactionCommander, etc.
    DateTime JoinedAt
);
