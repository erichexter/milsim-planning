namespace MilsimPlanning.Api.Models.Frequencies;

public class CreateFrequencyPoolRequest
{
    public List<FrequencyPoolEntryInputDto> Entries { get; set; } = [];
}

public class FrequencyPoolEntryInputDto
{
    public string Channel { get; set; } = null!;
    public string? DisplayGroup { get; set; }
    public int SortOrder { get; set; }
    public bool IsReserved { get; set; }
    public string? ReservedRole { get; set; }
}
