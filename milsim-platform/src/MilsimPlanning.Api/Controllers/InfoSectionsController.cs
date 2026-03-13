using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Models.Content;
using MilsimPlanning.Api.Services;

namespace MilsimPlanning.Api.Controllers;

[ApiController]
[Route("api/events/{eventId:guid}/info-sections")]
public class InfoSectionsController : ControllerBase
{
    private readonly IContentService _contentService;
    private readonly ICurrentUser _currentUser;
    private readonly IFileService _fileService;

    public InfoSectionsController(IContentService contentService, ICurrentUser currentUser, IFileService fileService)
    {
        _contentService = contentService;
        _currentUser = currentUser;
        _fileService = fileService;
    }

    // CONT-01: GET all info sections (ordered)
    [HttpGet]
    [Authorize(Policy = "RequirePlayer")]
    public async Task<IActionResult> GetInfoSections(Guid eventId)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);
        var sections = await _contentService.GetInfoSectionsAsync(eventId);
        return Ok(sections.Select(s => new
        {
            s.Id,
            s.EventId,
            s.Title,
            s.BodyMarkdown,
            s.Order,
            Attachments = s.Attachments.Select(a => new
            {
                a.Id,
                a.InfoSectionId,
                a.R2Key,
                a.FriendlyName,
                a.ContentType,
                a.FileSizeBytes
            })
        }));
    }

    // CONT-01: POST create info section
    [HttpPost]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<IActionResult> CreateInfoSection(Guid eventId, [FromBody] CreateInfoSectionRequest request)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { error = "Title is required" });

        try
        {
            var section = await _contentService.CreateInfoSectionAsync(eventId, request);
            return Created(
                $"/api/events/{eventId}/info-sections/{section.Id}",
                new { section.Id, section.EventId, section.Title, section.BodyMarkdown, section.Order }
            );
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ForbiddenException) { return Forbid(); }
    }

    // CONT-02: PUT update info section
    [HttpPut("{sectionId:guid}")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<IActionResult> UpdateInfoSection(Guid eventId, Guid sectionId, [FromBody] UpdateInfoSectionRequest request)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        try
        {
            await _contentService.UpdateInfoSectionAsync(eventId, sectionId, request);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ForbiddenException) { return Forbid(); }
    }

    // CONT-01: DELETE info section
    [HttpDelete("{sectionId:guid}")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<IActionResult> DeleteInfoSection(Guid eventId, Guid sectionId)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        try
        {
            await _contentService.DeleteInfoSectionAsync(eventId, sectionId);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ForbiddenException) { return Forbid(); }
    }

    // CONT-04: PATCH reorder info sections
    [HttpPatch("reorder")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<IActionResult> ReorderInfoSections(Guid eventId, [FromBody] ReorderSectionsRequest request)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        try
        {
            await _contentService.ReorderInfoSectionsAsync(eventId, request);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ForbiddenException) { return Forbid(); }
    }

    // CONT-03: GET upload URL for an attachment
    [HttpGet("{sectionId:guid}/attachments/upload-url")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<IActionResult> GetUploadUrl(Guid eventId, Guid sectionId, [FromQuery] UploadUrlRequest request)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        try
        {
            var result = await _contentService.GetUploadUrlAsync(eventId, sectionId, request);
            return Ok(new { result.UploadId, result.PresignedPutUrl, result.R2Key });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ForbiddenException) { return Forbid(); }
        catch (ValidationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    // CONT-03: POST confirm attachment upload
    [HttpPost("{sectionId:guid}/attachments/confirm")]
    [Authorize(Policy = "RequireFactionCommander")]
    public async Task<IActionResult> ConfirmAttachment(Guid eventId, Guid sectionId, [FromBody] ConfirmAttachmentRequest request)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        try
        {
            var attachment = await _contentService.ConfirmAttachmentAsync(eventId, sectionId, request);
            return Created(
                $"/api/events/{eventId}/info-sections/{sectionId}/attachments/{attachment.Id}",
                new { attachment.Id, attachment.InfoSectionId, attachment.FriendlyName, attachment.ContentType, attachment.FileSizeBytes }
            );
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ForbiddenException) { return Forbid(); }
    }

    // CONT-05: GET download URL for an attachment (generated on demand — never stored in DB)
    [HttpGet("{sectionId:guid}/attachments/{attachmentId:guid}/download-url")]
    [Authorize(Policy = "RequirePlayer")]
    public async Task<IActionResult> GetDownloadUrl(Guid eventId, Guid sectionId, Guid attachmentId)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        try
        {
            var attachment = await _contentService.GetAttachmentAsync(eventId, sectionId, attachmentId);
            if (attachment is null) return NotFound();

            // Generate on demand — not stored in DB
            var downloadUrl = _fileService.GenerateDownloadUrl(attachment.R2Key);
            return Ok(new { downloadUrl });
        }
        catch (ForbiddenException) { return Forbid(); }
    }
}
