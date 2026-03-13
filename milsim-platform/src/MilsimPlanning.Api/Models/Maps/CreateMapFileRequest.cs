namespace MilsimPlanning.Api.Models.Maps;

public record CreateMapFileRequest(string FileName, string ContentType, long FileSizeBytes, string FriendlyName, string? Instructions);
