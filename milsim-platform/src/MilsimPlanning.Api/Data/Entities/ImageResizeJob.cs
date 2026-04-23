namespace MilsimPlanning.Api.Data.Entities;

public enum ResizeStatus
{
    Queued,
    Processing,
    Completed,
    Failed
}

public class ImageResizeJob
{
    public Guid Id { get; set; }
    public Guid ImageUploadId { get; set; }
    public DateTime JobStartedAt { get; set; }
    public DateTime? JobCompletedAt { get; set; }
    public string TargetDimensions { get; set; } = null!; // "1280x720", "640x480", "320x240"
    public ResizeStatus ResizeStatus { get; set; } = ResizeStatus.Queued;
    public string? OutputR2Key { get; set; }
    public string? ErrorLog { get; set; }

    // Navigation property
    public ImageUpload ImageUpload { get; set; } = null!;
}
