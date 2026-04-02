namespace MilsimPlanning.Api.Models.Frequency;

public class SquadFrequencyDto
{
    public Guid SquadId { get; set; }
    public string? Primary { get; set; }
    public string? Backup { get; set; }
}
