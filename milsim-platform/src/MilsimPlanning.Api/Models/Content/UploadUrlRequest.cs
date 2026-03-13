namespace MilsimPlanning.Api.Models.Content;

public record UploadUrlRequest(string FileName, string ContentType, long FileSizeBytes);
