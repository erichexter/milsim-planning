namespace MilsimPlanning.Api.Models.Frequency;

public record SquadFrequencyDto(
    Guid SquadId,
    string SquadName,
    string? Primary,
    string? Backup
);

public record PlatoonFrequencyDto(
    Guid PlatoonId,
    string PlatoonName,
    string? Primary,
    string? Backup
);

public record CommandFrequencyDto(
    Guid FactionId,
    string? Primary,
    string? Backup
);

public record FrequencyVisibilityDto(
    SquadFrequencyDto? Squad,
    PlatoonFrequencyDto? Platoon,
    CommandFrequencyDto? Command
);

public record UpdateFrequencyRequest(
    string? Primary,
    string? Backup
);
