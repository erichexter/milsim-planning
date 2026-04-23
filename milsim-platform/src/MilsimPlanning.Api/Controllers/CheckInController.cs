using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Models.CheckIn;
using MilsimPlanning.Api.Services;

namespace MilsimPlanning.Api.Controllers;

/// <summary>
/// Kiosk check-in endpoints for recording participant scan events.
/// All endpoints require user to be a member of the specified event.
/// </summary>
[ApiController]
[Route("api/events/{eventId:guid}/check-in")]
[Authorize]
public class CheckInController : ControllerBase
{
    private readonly CheckInService _checkInService;
    private readonly ICurrentUser _currentUser;

    public CheckInController(CheckInService checkInService, ICurrentUser currentUser)
    {
        _checkInService = checkInService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Records a check-in event from a kiosk QR code scan.
    /// Returns 201 Created with participant confirmation details.
    /// </summary>
    [HttpPost("record-scan")]
    public async Task<ActionResult<CheckInRecordDto>> RecordScan(
        Guid eventId,
        [FromBody] RecordScanRequest request)
    {
        try
        {
            // Validate request
            if (string.IsNullOrWhiteSpace(request.QrCodeValue))
                return BadRequest(new { error = "QR code value is required" });

            // Check user has access to event
            ScopeGuard.AssertEventAccess(_currentUser, eventId);

            // Get kioskId from headers (optional)
            var kioskId = Request.Headers["X-Kiosk-Id"].ToString();

            // Record check-in
            var result = await _checkInService.RecordCheckInAsync(eventId, request.QrCodeValue, kioskId);
            return CreatedAtAction(nameof(RecordScan), new { eventId }, result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // Duplicate check-in
            return BadRequest(new { error = ex.Message });
        }
        catch (ForbiddenException ex)
        {
            return Forbid();
        }
    }
}
