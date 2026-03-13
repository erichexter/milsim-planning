namespace MilsimPlanning.Api.Models.Maps;

public record CreateExternalMapLinkRequest(string ExternalUrl, string? Instructions, string FriendlyName);
