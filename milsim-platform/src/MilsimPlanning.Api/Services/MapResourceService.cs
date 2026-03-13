using FluentValidation;
using Microsoft.EntityFrameworkCore;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Models.Maps;

namespace MilsimPlanning.Api.Services;

public interface IMapResourceService
{
    Task<MapResource> CreateExternalLinkAsync(Guid eventId, CreateExternalMapLinkRequest request);
    Task<UploadUrlResponse> GetUploadUrlAsync(Guid eventId, Guid mapResourceId, CreateMapFileRequest request);
    Task ConfirmFileUploadAsync(Guid eventId, Guid mapResourceId, ConfirmMapFileRequest request);
    Task<string> GetDownloadUrlAsync(Guid eventId, Guid mapResourceId);
    Task DeleteMapResourceAsync(Guid eventId, Guid mapResourceId);
    Task<List<MapResource>> ListMapResourcesAsync(Guid eventId);
}

public class MapResourceService : IMapResourceService
{
    private const long MaxFileSizeBytes = 10L * 1024 * 1024;

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IFileService _fileService;

    public MapResourceService(AppDbContext db, ICurrentUser currentUser, IFileService fileService)
    {
        _db = db;
        _currentUser = currentUser;
        _fileService = fileService;
    }

    public async Task<MapResource> CreateExternalLinkAsync(Guid eventId, CreateExternalMapLinkRequest request)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var evt = await _db.Events
            .Include(e => e.Faction)
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found");

        AssertCommanderAccess(evt.Faction);

        var order = await _db.MapResources
            .Where(r => r.EventId == eventId)
            .Select(r => (int?)r.Order)
            .MaxAsync() ?? -1;

        var resource = new MapResource
        {
            EventId = eventId,
            ExternalUrl = request.ExternalUrl,
            Instructions = request.Instructions,
            FriendlyName = request.FriendlyName,
            Order = order + 1
        };

        _db.MapResources.Add(resource);
        await _db.SaveChangesAsync();
        return resource;
    }

    public async Task<UploadUrlResponse> GetUploadUrlAsync(Guid eventId, Guid mapResourceId, CreateMapFileRequest request)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var evt = await _db.Events
            .Include(e => e.Faction)
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found");

        AssertCommanderAccess(evt.Faction);

        if (request.FileSizeBytes > MaxFileSizeBytes)
            throw new ValidationException($"File size {request.FileSizeBytes} bytes exceeds the 10 MB limit.");

        var uploadResponse = _fileService.GenerateUploadUrl(eventId, mapResourceId, request.ContentType, request.FileName);

        var mapResource = await _db.MapResources
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.Id == mapResourceId);

        if (mapResource is null)
        {
            var order = await _db.MapResources
                .Where(r => r.EventId == eventId)
                .Select(r => (int?)r.Order)
                .MaxAsync() ?? -1;

            mapResource = new MapResource
            {
                Id = mapResourceId,
                EventId = eventId,
                FriendlyName = request.FriendlyName,
                Instructions = request.Instructions,
                ContentType = request.ContentType,
                R2Key = uploadResponse.R2Key,
                ExternalUrl = null,
                Order = order + 1
            };

            _db.MapResources.Add(mapResource);
        }
        else
        {
            mapResource.FriendlyName = request.FriendlyName;
            mapResource.Instructions = request.Instructions;
            mapResource.ContentType = request.ContentType;
            mapResource.R2Key = uploadResponse.R2Key;
            mapResource.ExternalUrl = null;
        }

        await _db.SaveChangesAsync();
        return uploadResponse;
    }

    public async Task ConfirmFileUploadAsync(Guid eventId, Guid mapResourceId, ConfirmMapFileRequest request)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var evt = await _db.Events
            .Include(e => e.Faction)
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found");

        AssertCommanderAccess(evt.Faction);

        var resource = await _db.MapResources
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.Id == mapResourceId)
            ?? throw new KeyNotFoundException($"Map resource {mapResourceId} not found");

        if (!string.Equals(resource.R2Key, request.R2Key, StringComparison.Ordinal))
            throw new ValidationException("Confirmed R2 key does not match the pending upload.");

        resource.ContentType = request.ContentType;
        await _db.SaveChangesAsync();
    }

    public async Task<string> GetDownloadUrlAsync(Guid eventId, Guid mapResourceId)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var resource = await _db.MapResources
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.Id == mapResourceId)
            ?? throw new KeyNotFoundException($"Map resource {mapResourceId} not found");

        if (string.IsNullOrWhiteSpace(resource.R2Key))
            throw new KeyNotFoundException($"Map resource {mapResourceId} has no uploaded file");

        return _fileService.GenerateDownloadUrl(resource.R2Key);
    }

    public async Task DeleteMapResourceAsync(Guid eventId, Guid mapResourceId)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var evt = await _db.Events
            .Include(e => e.Faction)
            .FirstOrDefaultAsync(e => e.Id == eventId)
            ?? throw new KeyNotFoundException($"Event {eventId} not found");

        AssertCommanderAccess(evt.Faction);

        var resource = await _db.MapResources
            .FirstOrDefaultAsync(r => r.EventId == eventId && r.Id == mapResourceId)
            ?? throw new KeyNotFoundException($"Map resource {mapResourceId} not found");

        _db.MapResources.Remove(resource);
        await _db.SaveChangesAsync();
    }

    public async Task<List<MapResource>> ListMapResourcesAsync(Guid eventId)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        return await _db.MapResources
            .Where(r => r.EventId == eventId)
            .OrderBy(r => r.Order)
            .ToListAsync();
    }

    private void AssertCommanderAccess(Faction faction)
    {
        if (faction.CommanderId != _currentUser.UserId)
            throw new ForbiddenException("User is not the commander of this event's faction");
    }
}
