using CsvHelper.Configuration.Attributes;

namespace MilsimPlanning.Api.Models.CsvImport;

public class RosterImportRow
{
    [Name("name")]    public string Name { get; set; } = null!;
    [Name("email")]   public string Email { get; set; } = null!;
    [Name("callsign")] public string? Callsign { get; set; }
    [Name("team")]    public string? TeamAffiliation { get; set; }
}
