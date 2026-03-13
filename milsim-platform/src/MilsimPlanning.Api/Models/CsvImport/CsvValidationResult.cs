namespace MilsimPlanning.Api.Models.CsvImport;

public class CsvValidationResult
{
    public string? FatalError { get; set; }
    public int ValidCount { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public List<CsvRowError> Errors { get; set; } = [];
}
