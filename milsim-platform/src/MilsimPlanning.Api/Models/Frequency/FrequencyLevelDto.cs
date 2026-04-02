namespace MilsimPlanning.Api.Models.Frequency;

public class FrequencyLevelDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Primary { get; set; }
    public string? Backup { get; set; }
}
