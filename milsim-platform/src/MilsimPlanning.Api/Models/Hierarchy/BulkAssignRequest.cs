using System.ComponentModel.DataAnnotations;

namespace MilsimPlanning.Api.Models.Hierarchy;

/// <summary>
/// Assign multiple players to the same destination in one request.
/// Destination encoding: "squad:{guid}" or "platoon:{guid}"
/// </summary>
public record BulkAssignRequest(
    [Required, MinLength(1)] List<Guid> PlayerIds,
    [Required] string Destination
);
