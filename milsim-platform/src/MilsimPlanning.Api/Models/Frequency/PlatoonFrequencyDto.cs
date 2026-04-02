namespace MilsimPlanning.Api.Models.Frequency;

public class PlatoonFrequencyDto
{
    public Guid PlatoonId { get; set; }
    public string? Primary { get; set; }
    public string? Backup { get; set; }
}
