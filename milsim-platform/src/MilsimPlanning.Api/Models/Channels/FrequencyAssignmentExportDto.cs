namespace MilsimPlanning.Api.Models.Channels;

/// <summary>
/// Represents a single unit's frequency assignment for export
/// </summary>
public record FrequencyAssignmentExportDto(
    string Unit,                      // Unit identifier (e.g., "Squad-Alpha", "Platoon-Bravo", "Faction-Command")
    decimal? PrimaryFrequency,        // MHz, e.g. 30.025
    decimal? AlternateFrequency       // MHz, e.g. 30.050, or null if no alternate
);
