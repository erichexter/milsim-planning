namespace MilsimPlanning.Api.Models.Frequencies;

public record FrequencyOverviewDto(CommandFrequencyDto? Command, List<PlatoonFrequencyDto> Platoons, List<SquadFrequencyDto> Squads);
public record CommandFrequencyDto(string? Primary, string? Backup);
public record PlatoonFrequencyDto(Guid PlatoonId, string PlatoonName, string? Primary, string? Backup);
public record SquadFrequencyDto(Guid SquadId, string SquadName, Guid PlatoonId, string? Primary, string? Backup);
