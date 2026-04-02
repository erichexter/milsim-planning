namespace MilsimPlanning.Api.Models.Frequency;

public class FrequencyViewDto
{
    public FrequencyLevelDto? Command { get; set; }
    public FrequencyLevelDto[]? Platoons { get; set; }
    public FrequencyLevelDto[]? Squads { get; set; }
}

public class FrequencyLevelDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Primary { get; set; }
    public string? Backup { get; set; }
}
