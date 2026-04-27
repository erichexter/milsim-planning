namespace MilsimPlanning.Api.Models.Channels;

/// <summary>
/// Represents a radio channel with its assigned frequencies for export
/// </summary>
public record ChannelFrequencyExportDto(
    string Name,                                          // Channel name, e.g. "Command Net"
    string Scope,                                         // VHF, UHF, etc.
    List<FrequencyAssignmentExportDto> Assignments        // List of unit frequency assignments
);
