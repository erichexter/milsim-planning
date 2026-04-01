namespace MilsimPlanning.Api.Models.Frequencies;

public record FrequencyLevelDto(string? Primary, string? Backup);

public record EventFrequenciesDto(FrequencyLevelDto? Squad, FrequencyLevelDto? Platoon, FrequencyLevelDto? Command);
