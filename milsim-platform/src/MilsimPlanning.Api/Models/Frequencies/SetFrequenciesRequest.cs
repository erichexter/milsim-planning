namespace MilsimPlanning.Api.Models.Frequencies;

public record SetFrequenciesRequest(string? PrimaryFrequency, string? BackupFrequency);
