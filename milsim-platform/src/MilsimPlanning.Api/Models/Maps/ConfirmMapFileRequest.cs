namespace MilsimPlanning.Api.Models.Maps;

public record ConfirmMapFileRequest(string R2Key, string ContentType, long FileSizeBytes);
