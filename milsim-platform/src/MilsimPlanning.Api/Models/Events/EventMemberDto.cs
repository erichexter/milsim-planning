namespace MilsimPlanning.Api.Models.Events;

public record EventMemberDto(
    string UserId,
    string UserEmail,
    string Role,        // role name: "player", "faction_commander", "event_owner", etc.
    string RoleLabel,   // display label: "Player", "Faction Commander", "Event Owner", etc.
    Guid? FactionId,    // null for EventOwner and Player; set for commanders
    DateTime JoinedAt
);
