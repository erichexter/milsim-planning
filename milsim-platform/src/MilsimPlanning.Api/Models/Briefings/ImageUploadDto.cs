namespace MilsimPlanning.Api.Models.Briefings;

public class ImageUploadDto
{
    public Guid UploadId { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
}

public class ImageUploadStatusDto
{
    public Guid UploadId { get; set; }
    public string UploadStatus { get; set; } = null!;  // "Pending|Processing|Completed|Failed"
    public List<ResizeJobStatusDto> ResizeJobs { get; set; } = new();
}

public class ResizeJobStatusDto
{
    public Guid JobId { get; set; }
    public string Dimensions { get; set; } = null!;        // "1280x720"
    public string ResizeStatus { get; set; } = null!;      // "Queued|Processing|Completed|Failed"
    public DateTime? CompletedAt { get; set; }
}
