namespace MilsimPlanning.Api.Models.Frequencies;

public class FrequencyPoolDto
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public List<FrequencyPoolEntryDto> Entries { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
