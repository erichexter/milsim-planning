using Microsoft.EntityFrameworkCore;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Models.Briefings;

namespace MilsimPlanning.Api.Services;

public class BriefingService
{
    private readonly AppDbContext _db;

    public BriefingService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Creates a new Briefing with an auto-generated stable channelIdentifier (UUID).
    /// The channelIdentifier is immutable after creation — it is the stable "channel"
    /// that QR codes will reference across multiple briefings.
    /// </summary>
    public async Task<BriefingDto> CreateBriefingAsync(CreateBriefingRequest request)
    {
        var now = DateTime.UtcNow;

        var briefing = new Briefing
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            ChannelIdentifier = Guid.NewGuid().ToString(), // stable UUID, never editable
            PublicationState = "Draft",
            VersionETag = "etag-v1",
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.Briefings.Add(briefing);
        await _db.SaveChangesAsync();

        return ToDto(briefing);
    }

    /// <summary>Retrieves a single Briefing by its primary key.</summary>
    public async Task<BriefingDto?> GetByIdAsync(Guid id)
    {
        var briefing = await _db.Briefings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted);

        return briefing is null ? null : ToDto(briefing);
    }

    private static BriefingDto ToDto(Briefing b) => new(
        b.Id,
        b.Title,
        b.Description,
        b.ChannelIdentifier,
        b.PublicationState,
        b.VersionETag,
        b.CreatedAt,
        b.UpdatedAt
    );
}
