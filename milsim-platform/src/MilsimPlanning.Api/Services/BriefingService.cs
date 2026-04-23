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

    /// <summary>
    /// Returns a paginated list of non-deleted Briefings ordered by updatedAt descending.
    /// </summary>
    public async Task<BriefingListDto> ListBriefingsAsync(int limit, int offset)
    {
        var query = _db.Briefings
            .AsNoTracking()
            .Where(b => !b.IsDeleted)
            .OrderByDescending(b => b.UpdatedAt);

        var total = await query.CountAsync();

        var items = await query
            .Skip(offset)
            .Take(limit)
            .Select(b => new BriefingSummaryDto
            {
                Id = b.Id,
                Title = b.Title,
                Description = b.Description,
                ChannelIdentifier = b.ChannelIdentifier,
                PublicationState = b.PublicationState,
                UpdatedAt = b.UpdatedAt
            })
            .ToListAsync();

        return new BriefingListDto
        {
            Items = items,
            Pagination = new PaginationDto
            {
                Limit = limit,
                Offset = offset,
                Total = total
            }
        };
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
