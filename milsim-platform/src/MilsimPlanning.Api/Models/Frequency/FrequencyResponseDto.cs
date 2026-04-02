namespace MilsimPlanning.Api.Models.Frequency;

public class FrequencyResponseDto
{
    public FrequencyBandDto? Command { get; set; }
    public List<PlatoonFrequencyDto>? Platoons { get; set; }
    public List<SquadFrequencyDto>? Squads { get; set; }
}

public class FrequencyBandDto
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
