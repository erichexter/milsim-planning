namespace MilsimPlanning.Api.Models.Hierarchy;

/// <summary>
/// Request body for PATCH /api/event-players/{id}/role.
/// Pass null to clear the role.
/// </summary>
public record SetPlayerRoleRequest(string? Role);
