namespace MilsimPlanning.Api.Models.Frequency;

public class FrequenciesDto
{
    public FrequencyLevelDto? Squad { get; set; }
    public FrequencyLevelDto? Platoon { get; set; }
    public FrequencyLevelDto? Command { get; set; }
    public IReadOnlyList<FrequencyLevelDto>? AllPlatoons { get; set; }
    public IReadOnlyList<FrequencyLevelDto>? AllSquads { get; set; }
}
