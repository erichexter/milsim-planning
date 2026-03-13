using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Models.Maps;
using MilsimPlanning.Api.Services;

namespace MilsimPlanning.Api.Controllers;

[ApiController]
[Route("api/events/{eventId:guid}/map-resources")]
public class MapResourcesController : ControllerBase
{
    private readonly IMapResourceService _mapResourceService;
    private readonly ICurrentUser _currentUser;

    public MapResourcesController(IMapResourceService mapResourceService, ICurrentUser currentUser)
    {
        _mapResourceService = mapResourceService;
        _currentUser = currentUser;
    }

    [HttpPost("external")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<IActionResult> CreateExternalMapLinkAsync(Guid eventId, [FromBody] CreateExternalMapLinkRequest request)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        if (string.IsNullOrWhiteSpace(request.ExternalUrl))
            return BadRequest(new { error = "ExternalUrl is required" });

        try
        {
            var resource = await _mapResourceService.CreateExternalLinkAsync(eventId, request);
            return Created(
                $"/api/events/{eventId}/map-resources/{resource.Id}",
                new
                {
                    resource.Id,
                    resource.EventId,
                    resource.ExternalUrl,
                    resource.Instructions,
                    resource.FriendlyName,
                    resource.Order
                }
            );
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ForbiddenException)
        {
            return Forbid();
        }
    }

    [HttpGet]
    [Authorize(Policy = "RequirePlayer")]
    public async Task<IActionResult> ListMapResourcesAsync(Guid eventId)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var resources = await _mapResourceService.ListMapResourcesAsync(eventId);
        return Ok(resources.Select(r => new
        {
            r.Id,
            r.EventId,
            r.ExternalUrl,
            r.Instructions,
            r.FriendlyName,
            r.ContentType,
            r.Order,
            IsFile = !string.IsNullOrWhiteSpace(r.R2Key)
        }));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<IActionResult> DeleteMapResourceAsync(Guid eventId, Guid id)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        try
        {
            await _mapResourceService.DeleteMapResourceAsync(eventId, id);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ForbiddenException)
        {
            return Forbid();
        }
    }

    [HttpGet("{id:guid}/upload-url")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<IActionResult> GetUploadUrlAsync(Guid eventId, Guid id, [FromQuery] CreateMapFileRequest request)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        try
        {
            var result = await _mapResourceService.GetUploadUrlAsync(eventId, id, request);
            return Ok(new { result.UploadId, result.PresignedPutUrl, result.R2Key });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ForbiddenException)
        {
            return Forbid();
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/confirm")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<IActionResult> ConfirmUploadAsync(Guid eventId, Guid id, [FromBody] ConfirmMapFileRequest request)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        try
        {
            await _mapResourceService.ConfirmFileUploadAsync(eventId, id, request);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ForbiddenException)
        {
            return Forbid();
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}/download-url")]
    [Authorize(Policy = "RequirePlayer")]
    public async Task<IActionResult> GetDownloadUrlAsync(Guid eventId, Guid id)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        try
        {
            var downloadUrl = await _mapResourceService.GetDownloadUrlAsync(eventId, id);
            return Ok(new { downloadUrl });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ForbiddenException)
        {
            return Forbid();
        }
    }
}
