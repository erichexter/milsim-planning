namespace MilsimPlanning.Api.Data.Entities;

public enum ChannelScope
{
    VHF = 0,
    UHF = 1
}

public class RadioChannel
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string Name { get; set; } = null!;
    public string? CallSign { get; set; }
    public ChannelScope Scope { get; set; } = ChannelScope.VHF;
    public int Order { get; set; }
    public bool IsDeleted { get; set; } = false;

    public Event Event { get; set; } = null!;
    public ICollection<RadioChannelAssignment> Assignments { get; set; } = [];
}
