namespace MilsimPlanning.Api.Models.Channels;

public record CreateRadioChannelRequest(
    string Name,
    string? CallSign,
    string Scope
);
