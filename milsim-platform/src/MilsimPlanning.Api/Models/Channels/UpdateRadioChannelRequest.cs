namespace MilsimPlanning.Api.Models.Channels;

public record UpdateRadioChannelRequest(
    string Name,
    string Scope
);
