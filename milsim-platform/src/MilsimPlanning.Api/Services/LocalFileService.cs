namespace MilsimPlanning.Api.Services;

/// <summary>
/// Development-only IFileService that stores files on local disk under
/// wwwroot/dev-uploads/ and serves them via ASP.NET static files middleware.
/// Upload goes through a local proxy endpoint instead of a real presigned URL.
/// </summary>
public class LocalFileService : IFileService
{
    private readonly string _webRootPath;
    private readonly string _appUrl;

    public LocalFileService(IWebHostEnvironment env, IConfiguration config)
    {
        _webRootPath = env.WebRootPath
            ?? Path.Combine(env.ContentRootPath, "wwwroot");
        _appUrl = (config["AppUrl"] ?? "http://localhost:5000").TrimEnd('/');
    }

    public UploadUrlResponse GenerateUploadUrl(Guid eventId, Guid resourceId, string contentType, string fileName)
    {
        var uploadId = Guid.NewGuid();
        var safeFileName = System.Text.RegularExpressions.Regex.Replace(fileName, @"[^\w.\-]", "_");
        var r2Key = $"events/{eventId}/resources/{resourceId}/files/{uploadId}/{safeFileName}";

        // The "presigned" URL points at our local proxy endpoint
        var putUrl = $"{_appUrl}/api/dev/upload/{r2Key}";

        return new UploadUrlResponse(uploadId, putUrl, r2Key);
    }

    public string GenerateDownloadUrl(string r2Key)
    {
        // Files are served as static content from wwwroot/dev-uploads/
        return $"{_appUrl}/dev-uploads/{r2Key}";
    }

    /// <summary>
    /// Writes the uploaded bytes to disk. Called by DevUploadController.
    /// </summary>
    public async Task SaveAsync(string r2Key, Stream body)
    {
        var filePath = Path.Combine(_webRootPath, "dev-uploads", r2Key.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await using var fs = File.Create(filePath);
        await body.CopyToAsync(fs);
    }
}
