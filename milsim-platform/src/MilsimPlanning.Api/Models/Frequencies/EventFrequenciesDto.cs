namespace MilsimPlanning.Api.Models.Frequencies;

public class EventFrequenciesDto
{
    public FrequencyPairDto? Command { get; set; }
    public List<PlatoonFrequencyDto> Platoons { get; set; } = [];
    public List<SquadFrequencyDto> Squads { get; set; } = [];
}

public class FrequencyPairDto
{
    public string? Primary { get; set; }
    public string? Backup { get; set; }
}

public class PlatoonFrequencyDto
{
    public Guid PlatoonId { get; set; }
    public string PlatoonName { get; set; } = null!;
    public string? Primary { get; set; }
    public string? Backup { get; set; }
}

public class SquadFrequencyDto
{
    public Guid SquadId { get; set; }
    public string SquadName { get; set; } = null!;
    public Guid PlatoonId { get; set; }
    public string? Primary { get; set; }
    public string? Backup { get; set; }
}
