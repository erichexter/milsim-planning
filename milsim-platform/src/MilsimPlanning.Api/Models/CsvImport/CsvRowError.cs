namespace MilsimPlanning.Api.Models.CsvImport;

public enum Severity { Error, Warning }

public record CsvRowError(int Row, string Field, string Message, Severity Severity);
