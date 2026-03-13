using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Models.Content;

namespace MilsimPlanning.Api.Services;

public interface IContentService
{
    Task<InfoSection> CreateInfoSectionAsync(Guid eventId, CreateInfoSectionRequest request);
    Task UpdateInfoSectionAsync(Guid eventId, Guid sectionId, UpdateInfoSectionRequest request);
    Task DeleteInfoSectionAsync(Guid eventId, Guid sectionId);
    Task ReorderInfoSectionsAsync(Guid eventId, ReorderSectionsRequest request);
    Task<UploadUrlResponse> GetUploadUrlAsync(Guid eventId, Guid sectionId, UploadUrlRequest request);
    Task<InfoSectionAttachment> ConfirmAttachmentAsync(Guid eventId, Guid sectionId, ConfirmAttachmentRequest request);
    Task<InfoSectionAttachment?> GetAttachmentAsync(Guid eventId, Guid sectionId, Guid attachmentId);
    Task<List<InfoSection>> GetInfoSectionsAsync(Guid eventId);
}

public class ContentService : IContentService
{
    private const long MaxFileSizeBytes = 10L * 1024 * 1024; // 10 MB

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IFileService _fileService;

    public ContentService(AppDbContext db, ICurrentUser currentUser, IFileService fileService)
    {
        _db = db;
        _currentUser = currentUser;
        _fileService = fileService;
    }

    /// <summary>CONT-01: Create a new info section for an event.</summary>
    public async Task<InfoSection> CreateInfoSectionAsync(Guid eventId, CreateInfoSectionRequest request)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var evt = await _db.Events
            .Include(e => e.Faction)
            .Include(e => e.InfoSections)
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found");

        AssertCommanderAccess(evt.Faction);

        var section = new InfoSection
        {
            EventId = eventId,
            Title = request.Title,
            BodyMarkdown = request.BodyMarkdown,
            Order = evt.InfoSections.Count  // 0-based, appended at end
        };

        _db.InfoSections.Add(section);
        await _db.SaveChangesAsync();
        return section;
    }

    /// <summary>CONT-02: Update title and body of an info section.</summary>
    public async Task UpdateInfoSectionAsync(Guid eventId, Guid sectionId, UpdateInfoSectionRequest request)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var evt = await _db.Events
            .Include(e => e.Faction)
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found");

        AssertCommanderAccess(evt.Faction);

        var section = await _db.InfoSections
            .FirstOrDefaultAsync(s => s.Id == sectionId && s.EventId == eventId)
            ?? throw new KeyNotFoundException($"InfoSection {sectionId} not found");

        section.Title = request.Title;
        section.BodyMarkdown = request.BodyMarkdown;
        await _db.SaveChangesAsync();
    }

    /// <summary>CONT-01: Delete an info section and cascade attachments.</summary>
    public async Task DeleteInfoSectionAsync(Guid eventId, Guid sectionId)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var evt = await _db.Events
            .Include(e => e.Faction)
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found");

        AssertCommanderAccess(evt.Faction);

        var section = await _db.InfoSections
            .FirstOrDefaultAsync(s => s.Id == sectionId && s.EventId == eventId)
            ?? throw new KeyNotFoundException($"InfoSection {sectionId} not found");

        _db.InfoSections.Remove(section);
        await _db.SaveChangesAsync();
    }

    /// <summary>CONT-04: Full reassignment of section order — sets Order=0..N for all provided IDs.</summary>
    public async Task ReorderInfoSectionsAsync(Guid eventId, ReorderSectionsRequest request)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var evt = await _db.Events
            .Include(e => e.Faction)
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found");

        AssertCommanderAccess(evt.Faction);

        var sections = await _db.InfoSections
            .Where(s => s.EventId == eventId)
            .ToListAsync();

        // Full reassignment — loop i=0..N, set sections[i].Order = i
        for (int i = 0; i < request.OrderedIds.Count; i++)
        {
            var section = sections.FirstOrDefault(s => s.Id == request.OrderedIds[i]);
            if (section is not null)
                section.Order = i;
        }

        await _db.SaveChangesAsync();
    }

    /// <summary>CONT-03: Generate a pre-signed upload URL after validating file size and MIME type.</summary>
    public async Task<UploadUrlResponse> GetUploadUrlAsync(Guid eventId, Guid sectionId, UploadUrlRequest request)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var evt = await _db.Events
            .Include(e => e.Faction)
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found");

        AssertCommanderAccess(evt.Faction);

        // Server-side 10 MB enforcement (client may also enforce but server is authoritative)
        if (request.FileSizeBytes > MaxFileSizeBytes)
            throw new ValidationException($"File size {request.FileSizeBytes} bytes exceeds the 10 MB limit.");

        // MIME validation delegated to FileService (single source of truth: AllowedMimeTypes whitelist)
        return _fileService.GenerateUploadUrl(eventId, sectionId, request.ContentType, request.FileName);
    }

    /// <summary>CONT-03: Confirm an upload by saving the attachment record to DB.</summary>
    public async Task<InfoSectionAttachment> ConfirmAttachmentAsync(Guid eventId, Guid sectionId, ConfirmAttachmentRequest request)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var evt = await _db.Events
            .Include(e => e.Faction)
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found");

        AssertCommanderAccess(evt.Faction);

        var section = await _db.InfoSections
            .FirstOrDefaultAsync(s => s.Id == sectionId && s.EventId == eventId)
            ?? throw new KeyNotFoundException($"InfoSection {sectionId} not found");

        var attachment = new InfoSectionAttachment
        {
            InfoSectionId = sectionId,
            R2Key = request.R2Key,
            FriendlyName = request.FriendlyName,
            ContentType = request.ContentType,
            FileSizeBytes = request.FileSizeBytes
        };

        _db.InfoSectionAttachments.Add(attachment);
        await _db.SaveChangesAsync();
        return attachment;
    }

    /// <summary>CONT-05: Get an attachment record (for download-url generation).</summary>
    public async Task<InfoSectionAttachment?> GetAttachmentAsync(Guid eventId, Guid sectionId, Guid attachmentId)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        return await _db.InfoSectionAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.InfoSectionId == sectionId);
    }

    /// <summary>Get all info sections for an event, ordered by Order field.</summary>
    public async Task<List<InfoSection>> GetInfoSectionsAsync(Guid eventId)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        return await _db.InfoSections
            .Where(s => s.EventId == eventId)
            .OrderBy(s => s.Order)
            .Include(s => s.Attachments)
            .ToListAsync();
    }

    /// <summary>
    /// Assert that the current user is the commander of this event's faction.
    /// Throws ForbiddenException (HTTP 403) if not.
    /// </summary>
    private void AssertCommanderAccess(Faction faction)
    {
        if (faction.CommanderId != _currentUser.UserId)
            throw new ForbiddenException("User is not the commander of this event's faction");
    }
}
