using Microsoft.AspNetCore.Mvc;
using MilsimPlanning.Api.Services;

namespace MilsimPlanning.Api.Controllers;

/// <summary>
/// Development-only upload/download proxy. Receives the raw file body that the browser
/// would normally PUT directly to a presigned R2 URL, saves it to local disk, and
/// serves it back via GET. Only registered when LocalFileService is active.
/// </summary>
[ApiController]
public class DevUploadController : ControllerBase
{
    private readonly LocalFileService _localFileService;

    public DevUploadController(LocalFileService localFileService)
        => _localFileService = localFileService;

    [HttpPut("api/dev/upload/{*key}")]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> Upload(string key)
    {
        await _localFileService.SaveAsync(key, Request.Body);
        return Ok();
    }

    [HttpGet("api/dev/upload/{*key}")]
    public IActionResult Download(string key)
    {
        var filePath = _localFileService.GetFilePath(key);
        if (!System.IO.File.Exists(filePath))
            return NotFound();

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var contentType = ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png"            => "image/png",
            ".gif"            => "image/gif",
            ".webp"           => "image/webp",
            ".pdf"            => "application/pdf",
            ".svg"            => "image/svg+xml",
            _                 => "application/octet-stream"
        };
        return PhysicalFile(filePath, contentType, enableRangeProcessing: true);
    }
}
