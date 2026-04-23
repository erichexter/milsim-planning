using Microsoft.EntityFrameworkCore;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Models.Briefings;

namespace MilsimPlanning.Api.Services;

public class ImageOptimizationService
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".avif" };

    private static readonly string[] ResizeDimensions = ["1280x720", "640x480", "320x240"];

    private const long MaxFileSizeBytes = 50L * 1024 * 1024; // 50 MB

    private readonly AppDbContext _db;
    private readonly LocalFileService _fileService;

    public ImageOptimizationService(AppDbContext db, LocalFileService fileService)
    {
        _db = db;
        _fileService = fileService;
    }

    /// <summary>
    /// Validates the file, stores it locally, creates an ImageUpload record (status=Pending),
    /// and queues ImageResizeJob records for 3 standard dimensions.
    /// </summary>
    public async Task<ImageUploadDto> UploadImageAsync(
        Guid briefingId,
        IFormFile file,
        string uploadedById)
    {
        // Validate briefing exists
        var briefingExists = await _db.Briefings
            .AnyAsync(b => b.Id == briefingId && !b.IsDeleted);
        if (!briefingExists)
            throw new KeyNotFoundException($"Briefing {briefingId} not found.");

        // Validate file extension
        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
            throw new ArgumentException(
                $"File type '{ext}' is not allowed. Accepted: JPEG, PNG, WebP, AVIF.");

        // Validate file size
        if (file.Length > MaxFileSizeBytes)
            throw new ArgumentException(
                $"File size {file.Length} bytes exceeds the 50 MB maximum.");

        var uploadId = Guid.NewGuid();
        var r2Key = $"briefing/{briefingId}/{uploadId}/original";
        var now = DateTime.UtcNow;

        // Persist file to local storage (R2 fallback pattern)
        using (var stream = file.OpenReadStream())
        {
            await _fileService.SaveAsync(r2Key, stream);
        }

        // Create ImageUpload record
        var imageUpload = new ImageUpload
        {
            Id = uploadId,
            BriefingId = briefingId,
            OriginalFileName = file.FileName,
            FileSizeBytes = file.Length,
            UploadedAt = now,
            UploadedById = uploadedById,
            UploadStatus = UploadStatus.Pending,
            R2OriginalKey = r2Key
        };
        _db.ImageUploads.Add(imageUpload);

        // Queue ImageResizeJob records for Story 6 background worker
        await QueueImageResizeAsync(imageUpload, now);

        await _db.SaveChangesAsync();

        return new ImageUploadDto
        {
            UploadId = uploadId,
            Status = "Pending",
            CreatedAt = now
        };
    }

    /// <summary>
    /// Retrieves upload status including all associated resize job statuses.
    /// </summary>
    public async Task<ImageUploadStatusDto?> GetUploadStatusAsync(Guid briefingId, Guid uploadId)
    {
        var imageUpload = await _db.ImageUploads
            .AsNoTracking()
            .Include(u => u.ResizeJobs)
            .FirstOrDefaultAsync(u => u.Id == uploadId && u.BriefingId == briefingId);

        if (imageUpload is null) return null;

        return new ImageUploadStatusDto
        {
            UploadId = imageUpload.Id,
            UploadStatus = imageUpload.UploadStatus.ToString(),
            ResizeJobs = imageUpload.ResizeJobs.Select(j => new ResizeJobStatusDto
            {
                JobId = j.Id,
                Dimensions = j.TargetDimensions,
                ResizeStatus = j.ResizeStatus.ToString(),
                CompletedAt = j.JobCompletedAt
            }).ToList()
        };
    }

    // Creates ImageResizeJob records for 3 standard variants — to be processed by Story 6 worker
    private Task QueueImageResizeAsync(ImageUpload imageUpload, DateTime now)
    {
        foreach (var dimensions in ResizeDimensions)
        {
            var job = new ImageResizeJob
            {
                Id = Guid.NewGuid(),
                ImageUploadId = imageUpload.Id,
                JobStartedAt = now,
                TargetDimensions = dimensions,
                ResizeStatus = ResizeStatus.Queued
            };
            _db.ImageResizeJobs.Add(job);
            imageUpload.ResizeJobs.Add(job);
        }
        return Task.CompletedTask;
    }
}
