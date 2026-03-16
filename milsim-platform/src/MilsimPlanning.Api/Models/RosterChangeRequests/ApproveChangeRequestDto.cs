namespace MilsimPlanning.Api.Models.RosterChangeRequests;

public class ApproveChangeRequestDto
{
    public Guid PlatoonId { get; set; }
    public Guid SquadId { get; set; }
    public string? CommanderNote { get; set; }
}
