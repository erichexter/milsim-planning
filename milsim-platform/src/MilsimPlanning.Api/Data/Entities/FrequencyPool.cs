namespace MilsimPlanning.Api.Data.Entities;

public class FrequencyPool
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Event Event { get; set; } = null!;
    public ICollection<FrequencyPoolEntry> Entries { get; set; } = [];
}
