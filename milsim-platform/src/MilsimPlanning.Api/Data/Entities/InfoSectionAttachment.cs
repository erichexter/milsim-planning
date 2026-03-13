namespace MilsimPlanning.Api.Data.Entities;

public class InfoSectionAttachment
{
    public Guid Id { get; set; }
    public Guid InfoSectionId { get; set; }
    public string R2Key { get; set; } = null!;
    public string FriendlyName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long FileSizeBytes { get; set; }
    public InfoSection Section { get; set; } = null!;
}
