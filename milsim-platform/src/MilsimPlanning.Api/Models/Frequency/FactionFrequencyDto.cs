namespace MilsimPlanning.Api.Models.Frequency;

public class FactionFrequencyDto
{
    public Guid FactionId { get; set; }
    public string? Primary { get; set; }
    public string? Backup { get; set; }
}
