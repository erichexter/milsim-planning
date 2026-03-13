namespace MilsimPlanning.Api.Data.Entities;

public class NotificationBlast
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string Subject { get; set; } = null!;
    public string Body { get; set; } = null!;
    public DateTime SentAt { get; set; }
    public int RecipientCount { get; set; }
    public Event Event { get; set; } = null!;
}
