using Microsoft.AspNetCore.Mvc;
using MilsimPlanning.Api.Services;

namespace MilsimPlanning.Api.Controllers;

/// <summary>
/// Development-only upload proxy. Receives the raw file body that the browser
/// would normally PUT directly to a presigned R2 URL, and saves it to local disk.
/// Only registered when LocalFileService is active (R2 credentials not configured).
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
}
