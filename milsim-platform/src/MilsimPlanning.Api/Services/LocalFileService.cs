namespace MilsimPlanning.Api.Services;

/// <summary>
/// Development-only IFileService that stores files on local disk under
/// wwwroot/dev-uploads/ and serves them via ASP.NET static files middleware.
/// Upload goes through a local proxy endpoint instead of a real presigned URL.
/// </summary>
public class LocalFileService : IFileService
{
    private readonly string _storageRoot;
    private readonly string _appUrl;

    public LocalFileService(IWebHostEnvironment env, IConfiguration config)
    {
        // Store uploads under ContentRootPath/dev-uploads (works in Docker where WebRootPath is null)
        _storageRoot = Path.Combine(env.ContentRootPath, "dev-uploads");
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
        // Served back through DevUploadController GET, proxied via Vite
        return $"{_appUrl}/api/dev/upload/{r2Key}";
    }

    /// <summary>
    /// Returns the absolute on-disk path for a given r2Key.
    /// </summary>
    public string GetFilePath(string r2Key)
        => Path.Combine(_storageRoot, r2Key.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>
    /// Writes the uploaded bytes to disk. Called by DevUploadController.
    /// </summary>
    public async Task SaveAsync(string r2Key, Stream body)
    {
        var filePath = GetFilePath(r2Key);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await using var fs = File.Create(filePath);
        await body.CopyToAsync(fs);
    }
}
