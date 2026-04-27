namespace MilsimPlanning.Api.Models.AuditLogs;

public class GetFrequencyAuditLogsRequest
{
    // Pagination
    public int Limit { get; set; } = 20;
    public int Offset { get; set; } = 0;

    // Filtering
    public string? UnitName { get; set; }  // Optional filter by unit name (AC-07)
    public DateTime? StartDate { get; set; }  // Optional filter by date range (AC-07)
    public DateTime? EndDate { get; set; }

    // Sorting
    public string SortBy { get; set; } = "timestamp";  // "timestamp", "unitName", "actionType"
    public string SortOrder { get; set; } = "desc";  // "asc" or "desc" (AC-02: user configurable)
}
