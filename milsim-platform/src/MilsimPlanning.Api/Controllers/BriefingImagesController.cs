using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MilsimPlanning.Api.Models.Briefings;
using MilsimPlanning.Api.Services;
using System.Security.Claims;

namespace MilsimPlanning.Api.Controllers;

[ApiController]
[Route("api/v1/briefings")]
[Authorize]
public class BriefingImagesController : ControllerBase
{
    private readonly ImageOptimizationService _imageService;

    public BriefingImagesController(ImageOptimizationService imageService)
        => _imageService = imageService;

    // AC-02: POST /api/v1/briefings/{briefingId}/images
    // Accepts multipart/form-data with an image file; returns 202 Accepted with ImageUploadDto
    [HttpPost("{briefingId:guid}/images")]
    [Authorize(Policy = "BriefingAdmin")]
    [RequestSizeLimit(52_428_800)] // 50 MB + overhead
    public async Task<ActionResult<ImageUploadDto>> UploadImage(
        Guid briefingId,
        IFormFile file)
    {
        if (file is null || file.Length == 0)
            return Problem(
                title: "Bad Request",
                detail: "No file provided.",
                statusCode: 400);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (userId is null)
            return Problem(title: "Unauthorized", detail: "Authentication required.", statusCode: 401);

        try
        {
            var dto = await _imageService.UploadImageAsync(briefingId, file, userId);
            return StatusCode(202, dto);
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(title: "Not Found", detail: ex.Message, statusCode: 404);
        }
        catch (ArgumentException ex)
        {
            return Problem(title: "Bad Request", detail: ex.Message, statusCode: 400);
        }
    }

    // AC-05: GET /api/v1/briefings/{briefingId}/images/{uploadId}
    // Returns ImageUploadStatusDto with resize job statuses for polling
    [HttpGet("{briefingId:guid}/images/{uploadId:guid}")]
    [Authorize(Policy = "BriefingAdmin")]
    public async Task<ActionResult<ImageUploadStatusDto>> GetUploadStatus(
        Guid briefingId,
        Guid uploadId)
    {
        var dto = await _imageService.GetUploadStatusAsync(briefingId, uploadId);

        if (dto is null)
            return Problem(
                title: "Not Found",
                detail: $"Image upload {uploadId} not found.",
                statusCode: 404);

        return Ok(dto);
    }
}
