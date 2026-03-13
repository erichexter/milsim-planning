using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Models.CsvImport;
using MilsimPlanning.Api.Services;

namespace MilsimPlanning.Api.Controllers;

/// <summary>
/// Roster import endpoints for faction commanders.
/// POST validate — per-row CSV validation, NO database writes (ROST-01, ROST-02, ROST-03)
/// POST commit  — upsert players by email, preserve squad assignments, trigger invites (ROST-04, ROST-05, ROST-06)
/// </summary>
[ApiController]
[Route("api/events/{eventId:guid}/roster")]
[Authorize(Policy = "RequireFactionCommander")]
public class RosterController : ControllerBase
{
    private readonly RosterService _rosterService;

    public RosterController(RosterService rosterService)
        => _rosterService = rosterService;

    /// <summary>
    /// POST /api/events/{eventId}/roster/validate
    /// Validate all CSV rows and return structured errors. Never writes to DB.
    /// </summary>
    [HttpPost("validate")]
    [RequestSizeLimit(10 * 1024 * 1024)]  // 10MB — 400 rows is well under 1MB
    public async Task<ActionResult<CsvValidationResult>> Validate(
        Guid eventId,
        IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file uploaded" });

        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "File must be a .csv" });

        var result = await _rosterService.ValidateRosterCsvAsync(file, eventId);
        return Ok(result);
    }

    /// <summary>
    /// POST /api/events/{eventId}/roster/commit
    /// Upsert players by email, preserve squad assignments, trigger invitation emails.
    /// Returns 422 if CSV has errors (defense-in-depth re-validation).
    /// </summary>
    [HttpPost("commit")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Commit(Guid eventId, IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file uploaded" });

        try
        {
            await _rosterService.CommitRosterCsvAsync(file, eventId);
            return NoContent();
        }
        catch (RosterValidationException)
        {
            // Re-validate found errors — return full validation result for the client
            var result = await _rosterService.ValidateRosterCsvAsync(file, eventId);
            return UnprocessableEntity(result);
        }
        catch (ForbiddenException)
        {
            return Forbid();
        }
    }
}
