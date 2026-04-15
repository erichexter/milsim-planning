using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Models.Rsvp;
using MilsimPlanning.Api.Services;

namespace MilsimPlanning.Api.Controllers;

[ApiController]
public class RsvpController : ControllerBase
{
    private readonly RsvpService _rsvpService;

    public RsvpController(RsvpService rsvpService)
        => _rsvpService = rsvpService;

    [HttpPut("api/events/{eventId:guid}/rsvp")]
    [Authorize]
    public async Task<ActionResult<RsvpDto>> SetRsvp(Guid eventId, [FromBody] SetRsvpRequest request)
    {
        try
        {
            var result = await _rsvpService.SetRsvpAsync(eventId, request);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return Problem(title: "Not Found", detail: ex.Message, statusCode: 404); }
        catch (ForbiddenException ex) { return Problem(title: "Forbidden", detail: ex.Message, statusCode: 403); }
        catch (ArgumentException ex) { return Problem(title: "Bad Request", detail: ex.Message, statusCode: 400); }
    }

    [HttpGet("api/events/{eventId:guid}/rsvp")]
    [Authorize]
    public async Task<IActionResult> GetRsvp(Guid eventId)
    {
        try
        {
            var result = await _rsvpService.GetRsvpAsync(eventId);
            return new JsonResult(result) { StatusCode = 200 };
        }
        catch (KeyNotFoundException ex) { return Problem(title: "Not Found", detail: ex.Message, statusCode: 404); }
        catch (ForbiddenException ex) { return Problem(title: "Forbidden", detail: ex.Message, statusCode: 403); }
    }
}
