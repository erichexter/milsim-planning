namespace MilsimPlanning.Api.Data.Entities;

public class InfoSection
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string Title { get; set; } = null!;
    public string? BodyMarkdown { get; set; }
    public int Order { get; set; }
    public Event Event { get; set; } = null!;
    public ICollection<InfoSectionAttachment> Attachments { get; set; } = [];
}
