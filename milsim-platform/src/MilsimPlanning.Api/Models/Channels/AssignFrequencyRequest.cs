namespace MilsimPlanning.Api.Models.Channels;

public record AssignFrequencyRequest(
    decimal? Primary,
    decimal? Alternate,
    bool OverrideValidation = false
);
