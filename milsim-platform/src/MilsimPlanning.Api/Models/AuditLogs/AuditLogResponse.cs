namespace MilsimPlanning.Api.Models.AuditLogs;

public class AuditLogResponse
{
    public IReadOnlyList<FrequencyAuditLogDto> Entries { get; set; } = [];
    public int Total { get; set; }
    public int Limit { get; set; }
    public int Offset { get; set; }
}
