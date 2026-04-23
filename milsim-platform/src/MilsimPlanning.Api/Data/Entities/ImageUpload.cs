namespace MilsimPlanning.Api.Data.Entities;

public enum UploadStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}

public class ImageUpload
{
    public Guid Id { get; set; }
    public Guid BriefingId { get; set; }
    public string OriginalFileName { get; set; } = null!;
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; }
    public string UploadedById { get; set; } = null!;  // FK → AspNetUsers.Id (string)
    public UploadStatus UploadStatus { get; set; } = UploadStatus.Pending;
    public string R2OriginalKey { get; set; } = null!; // s3://bucket/briefing/{BriefingId}/{Id}/original

    // Navigation properties
    public Briefing Briefing { get; set; } = null!;
    public AppUser UploadedBy { get; set; } = null!;
    public ICollection<ImageResizeJob> ResizeJobs { get; set; } = new List<ImageResizeJob>();
}
