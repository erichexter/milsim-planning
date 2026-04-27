using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Models.AuditLogs;
using MilsimPlanning.Api.Services;

namespace MilsimPlanning.Api.Controllers;

[ApiController]
[Route("api/events/{eventId:guid}/audit-logs")]
[Authorize(Policy = "RequirePlayer")]
public class AuditLogController : ControllerBase
{
    private readonly AuditLogService _auditLogService;

    public AuditLogController(AuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    // ── GET /api/events/{eventId}/audit-logs ──────────────────────────────────

    /// <summary>
    /// Get frequency assignment audit log entries for an operation (AC-01, AC-02, AC-07)
    /// </summary>
    /// <param name="eventId">Event ID</param>
    /// <param name="request">Query parameters for filtering, sorting, and pagination</param>
    /// <returns>Paginated list of audit log entries</returns>
    [HttpGet]
    [ProducesResponseType(typeof(AuditLogResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuditLogResponse>> GetAuditLogs(
        Guid eventId,
        [FromQuery] GetFrequencyAuditLogsRequest? request = null)
    {
        try
        {
            request ??= new GetFrequencyAuditLogsRequest();
            var (entries, total) = await _auditLogService.GetAuditLogsAsync(eventId, request);

            var response = new AuditLogResponse
            {
                Entries = entries,
                Total = total,
                Limit = request.Limit,
                Offset = request.Offset,
            };

            return Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Not Found",
                Detail = $"Event {eventId} not found.",
                Status = StatusCodes.Status404NotFound,
            });
        }
        catch (ForbiddenException ex)
        {
            return Problem(
                title: "Forbidden",
                detail: ex.Message,
                statusCode: StatusCodes.Status403Forbidden);
        }
    }
}
