using Amazon.S3;
using Amazon.S3.Model;
using FluentValidation;

namespace MilsimPlanning.Api.Services;

public interface IFileService
{
    UploadUrlResponse GenerateUploadUrl(Guid eventId, Guid resourceId, string contentType, string fileName);
    string GenerateDownloadUrl(string r2Key);
}

public record UploadUrlResponse(Guid UploadId, string PresignedPutUrl, string R2Key);

public class FileService : IFileService
{
    private static readonly HashSet<string> AllowedMimeTypes = new()
    {
        "application/pdf",
        "image/jpeg",
        "image/png",
        "image/gif",
        "application/vnd.google-earth.kmz",
        "application/vnd.google-earth.kml+xml",
        "application/zip"
    };

    private readonly IAmazonS3 _s3;
    private readonly string _bucket;

    public FileService(IAmazonS3 s3, IConfiguration config)
    {
        _s3 = s3;
        _bucket = config["R2:BucketName"]
            ?? throw new InvalidOperationException("R2:BucketName not configured");
    }

    public UploadUrlResponse GenerateUploadUrl(Guid eventId, Guid resourceId, string contentType, string fileName)
    {
        if (!AllowedMimeTypes.Contains(contentType))
            throw new ValidationException($"File type '{contentType}' is not permitted.");

        var uploadId = Guid.NewGuid();
        // Sanitize filename for use as an S3/R2 key — spaces and special chars can
        // break presigned URL signatures. The original friendly name is stored separately.
        var safeFileName = System.Text.RegularExpressions.Regex.Replace(fileName, @"[^\w.\-]", "_");
        var r2Key = $"events/{eventId}/resources/{resourceId}/files/{uploadId}/{safeFileName}";

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = r2Key,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.AddMinutes(15),
            ContentType = contentType  // baked into signature — browser MUST match
        };

        var url = _s3.GetPreSignedURL(request);
        return new UploadUrlResponse(uploadId, url, r2Key);
    }

    public string GenerateDownloadUrl(string r2Key)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = r2Key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddHours(1)  // 1-hour TTL
        };
        return _s3.GetPreSignedURL(request);
    }
}
