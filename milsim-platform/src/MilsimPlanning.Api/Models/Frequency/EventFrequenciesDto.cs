namespace MilsimPlanning.Api.Models.Frequency;

public class EventFrequenciesDto
{
    public FrequencyLevelDto? Squad { get; set; }
    public FrequencyLevelDto? Platoon { get; set; }
    public FrequencyLevelDto? Command { get; set; }
}
