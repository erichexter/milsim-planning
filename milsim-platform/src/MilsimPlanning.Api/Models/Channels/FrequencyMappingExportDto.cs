using System.Text.Json.Serialization;

namespace MilsimPlanning.Api.Models.Channels;

/// <summary>
/// Root DTO for frequency mapping export
/// Serialized to JSON and sent to client for download
/// </summary>
public record FrequencyMappingExportDto(
    [property: JsonPropertyName("operation")]
    string OperationName,

    [property: JsonPropertyName("export_timestamp")]
    string ExportTimestamp,

    [property: JsonPropertyName("channels")]
    List<ChannelFrequencyExportDto> Channels
);
