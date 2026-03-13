namespace MilsimPlanning.Api.Models.Content;

public record ConfirmAttachmentRequest(string R2Key, string FriendlyName, string ContentType, long FileSizeBytes);
