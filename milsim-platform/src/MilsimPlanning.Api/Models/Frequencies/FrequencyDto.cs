namespace MilsimPlanning.Api.Models.Frequencies;

public class FrequencyDto
{
    public FrequencyLevelDto? Squad   { get; set; }
    public FrequencyLevelDto? Platoon { get; set; }
    public FrequencyLevelDto? Command { get; set; }
}
