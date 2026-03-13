namespace MilsimPlanning.Api.Data.Entities;

public class MapResource
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string? ExternalUrl { get; set; }
    public string? Instructions { get; set; }
    public string? R2Key { get; set; }
    public string? FriendlyName { get; set; }
    public string? ContentType { get; set; }
    public int Order { get; set; }
    public Event Event { get; set; } = null!;
}
