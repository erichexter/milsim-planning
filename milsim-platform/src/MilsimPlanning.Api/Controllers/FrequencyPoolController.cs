using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MilsimPlanning.Api.Models.Frequencies;
using MilsimPlanning.Api.Services;

namespace MilsimPlanning.Api.Controllers;

[ApiController]
[Route("api/events/{eventId:guid}/frequency-pool")]
[Authorize]
public class FrequencyPoolController : ControllerBase
{
    private readonly FrequencyPoolService _frequencyPoolService;

    public FrequencyPoolController(FrequencyPoolService frequencyPoolService)
    {
        _frequencyPoolService = frequencyPoolService;
    }

    /// <summary>
    /// GET /api/events/{eventId}/frequency-pool
    /// Returns organizer-configured frequency pool. All authorized event roster members can read.
    /// </summary>
    /// <remarks>
    /// AC-02: Organizer enters a list of frequencies (e.g., "152.4 MHz", "152.5 MHz", "154.2 MHz")
    /// AC-05: Pool is stored in database scoped to the specific event
    /// AC-07: Confirmation message displays total frequencies available to factions (total pool minus reserved count)
    /// </remarks>
    [HttpGet]
    [ProduceResponseType(typeof(FrequencyPoolDto), StatusCodes.Status200OK)]
    [ProduceResponseType(StatusCodes.Status404NotFound)]
    [ProduceResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<FrequencyPoolDto>> GetFrequencyPool(Guid eventId)
    {
        try
        {
            var pool = await _frequencyPoolService.GetFrequencyPoolAsync(eventId);

            if (pool == null)
            {
                return NotFound(new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                    Title = "Not Found",
                    Detail = "Frequency pool not yet configured for this event.",
                    Status = StatusCodes.Status404NotFound
                });
            }

            return Ok(pool);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// PUT /api/events/{eventId}/frequency-pool
    /// Organizer creates or updates frequency pool.
    /// </summary>
    /// <remarks>
    /// AC-01: Organizer navigates to event settings and clicks "Configure Frequency Pool"
    /// AC-02: Organizer enters a list of frequencies as comma-separated values or one per line
    /// AC-03: Organizer designates 3 frequencies as reserved (safety net, medical net, event control)
    /// AC-04: System validates that all frequencies in the pool are unique (no duplicates)
    /// AC-05: Pool is stored in database scoped to the specific event
    /// AC-06: Once an event enters the submission window, the frequency pool becomes read-only
    /// AC-07: Confirmation message displays total frequencies available to factions
    /// </remarks>
    [HttpPut]
    [ProduceResponseType(typeof(FrequencyPoolDto), StatusCodes.Status200OK)]
    [ProduceResponseType(StatusCodes.Status400BadRequest)]
    [ProduceResponseType(StatusCodes.Status403Forbidden)]
    [ProduceResponseType(StatusCodes.Status409Conflict)]
    [ProduceResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<FrequencyPoolDto>> CreateOrUpdateFrequencyPool(
        Guid eventId,
        [FromBody] CreateFrequencyPoolRequest request,
        [FromQuery] bool force = false)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var pool = await _frequencyPoolService.CreateOrUpdateFrequencyPoolAsync(eventId, request, force);

            return Ok(pool);
        }
        catch (ArgumentException ex)
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.22",
                Title = "Unprocessable Entity",
                Detail = ex.Message,
                Status = StatusCodes.Status422UnprocessableEntity
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8",
                Title = "Conflict",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
    }
}
