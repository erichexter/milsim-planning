namespace MilsimPlanning.Api.Models.Frequencies;

public class FrequencyReadDto
{
    public SquadFrequencyDto? Squad { get; set; }
    public PlatoonFrequencyDto? Platoon { get; set; }
    public CommandFrequencyDto? Command { get; set; }
    public AllFrequenciesDto? AllFrequencies { get; set; }
}

public class SquadFrequencyDto
{
    public Guid SquadId { get; set; }
    public string SquadName { get; set; } = null!;
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

public class CommandFrequencyDto
{
    public Guid FactionId { get; set; }
    public string FactionName { get; set; } = null!;
    public string? Primary { get; set; }
    public string? Backup { get; set; }
}

public class AllFrequenciesDto
{
    public CommandFrequencyDto Command { get; set; } = null!;
    public List<PlatoonFrequencyDto> Platoons { get; set; } = [];
    public List<AllFrequenciesSquadDto> Squads { get; set; } = [];
}

public class AllFrequenciesSquadDto
{
    public Guid SquadId { get; set; }
    public string SquadName { get; set; } = null!;
    public string PlatoonName { get; set; } = null!;
    public string? Primary { get; set; }
    public string? Backup { get; set; }
}

public class UpdateFrequencyRequest
{
    public string? Primary { get; set; }
    public string? Backup { get; set; }
}
