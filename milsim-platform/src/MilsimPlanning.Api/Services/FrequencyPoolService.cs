using Microsoft.EntityFrameworkCore;
using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Models.Frequencies;

namespace MilsimPlanning.Api.Services;

public class FrequencyPoolService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public FrequencyPoolService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    /// <summary>
    /// GET /api/events/{eventId}/frequency-pool
    /// Returns organizer-configured frequency pool. All authorized event roster members can read.
    /// </summary>
    public async Task<FrequencyPoolDto?> GetFrequencyPoolAsync(Guid eventId)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        var pool = await _db.FrequencyPools
            .Include(fp => fp.Entries)
            .FirstOrDefaultAsync(fp => fp.EventId == eventId);

        if (pool == null)
            return null;

        return MapToDto(pool);
    }

    /// <summary>
    /// PUT /api/events/{eventId}/frequency-pool
    /// Organizer creates or updates frequency pool.
    /// </summary>
    public async Task<FrequencyPoolDto> CreateOrUpdateFrequencyPoolAsync(
        Guid eventId,
        CreateFrequencyPoolRequest request,
        bool force = false)
    {
        ScopeGuard.AssertEventAccess(_currentUser, eventId);

        // Load event and faction for authorization and AC-06 validation
        var evt = await _db.Events
            .Include(e => e.Faction)
            .FirstOrDefaultAsync(e => e.Id == eventId);

        if (evt == null)
            throw new ArgumentException($"Event {eventId} not found");

        // Validate organizer role: Only faction commander (organizer) can manage the frequency pool
        if (evt.Faction?.CommanderId != _currentUser.UserId)
            throw new UnauthorizedAccessException("Only the event organizer can manage the frequency pool");

        // Validate entries
        ValidateEntries(request.Entries);

        // Check if pool already exists
        var existingPool = await _db.FrequencyPools
            .Include(fp => fp.Entries)
            .FirstOrDefaultAsync(fp => fp.EventId == eventId);

        // AC-06: Once an event enters the submission window (Published), the frequency pool becomes read-only
        if (existingPool != null && evt.Status == EventStatus.Published)
            throw new InvalidOperationException("Frequency pool is read-only after event publication");

        if (existingPool != null)
        {
            // Update existing pool
            existingPool.UpdatedAt = DateTime.UtcNow;
            existingPool.Entries.Clear();

            foreach (var entryInput in request.Entries)
            {
                existingPool.Entries.Add(new FrequencyPoolEntry
                {
                    Id = Guid.NewGuid(),
                    Channel = NormalizeChannel(entryInput.Channel),
                    DisplayGroup = entryInput.DisplayGroup,
                    SortOrder = entryInput.SortOrder,
                    IsReserved = entryInput.IsReserved,
                    ReservedRole = entryInput.ReservedRole
                });
            }

            _db.FrequencyPools.Update(existingPool);
        }
        else
        {
            // Create new pool
            var newPool = new FrequencyPool
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Entries = new List<FrequencyPoolEntry>()
            };

            foreach (var entryInput in request.Entries)
            {
                newPool.Entries.Add(new FrequencyPoolEntry
                {
                    Id = Guid.NewGuid(),
                    FrequencyPoolId = newPool.Id,
                    Channel = NormalizeChannel(entryInput.Channel),
                    DisplayGroup = entryInput.DisplayGroup,
                    SortOrder = entryInput.SortOrder,
                    IsReserved = entryInput.IsReserved,
                    ReservedRole = entryInput.ReservedRole
                });
            }

            _db.FrequencyPools.Add(newPool);
        }

        await _db.SaveChangesAsync();

        var savedPool = await _db.FrequencyPools
            .Include(fp => fp.Entries)
            .FirstOrDefaultAsync(fp => fp.EventId == eventId);

        return MapToDto(savedPool!);
    }

    /// <summary>
    /// Validate frequency pool entries according to acceptance criteria.
    /// AC-04: System validates that all frequencies in the pool are unique (no duplicates)
    /// AC-03: Organizer designates 3 frequencies as reserved (safety net, medical net, event control)
    /// </summary>
    private void ValidateEntries(List<FrequencyPoolEntryInputDto> entries)
    {
        if (entries == null || entries.Count == 0)
            throw new ArgumentException("At least one frequency entry is required.");

        // AC-04: Check for duplicate channels (normalize before comparison)
        var normalizedChannels = entries
            .Select(e => NormalizeChannel(e.Channel))
            .ToList();

        var duplicates = normalizedChannels
            .GroupBy(c => c)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Any())
            throw new ArgumentException($"Duplicate frequencies found: {string.Join(", ", duplicates)}");

        // AC-03: Validate reserved roles if IsReserved=true
        var validReservedRoles = new[] { "Safety", "Medical", "Control" };

        foreach (var entry in entries)
        {
            if (entry.IsReserved)
            {
                if (string.IsNullOrWhiteSpace(entry.ReservedRole) ||
                    !validReservedRoles.Contains(entry.ReservedRole))
                {
                    throw new ArgumentException(
                        $"Reserved frequency '{entry.Channel}' must have a valid role: {string.Join(", ", validReservedRoles)}");
                }
            }

            if (!entry.IsReserved && !string.IsNullOrWhiteSpace(entry.ReservedRole))
            {
                throw new ArgumentException(
                    $"Non-reserved frequency '{entry.Channel}' cannot have a reserved role.");
            }
        }
    }

    /// <summary>
    /// Normalize channel format: lowercase, trimmed.
    /// Example: "  152.4 MHz  " → "152.4 mhz"
    /// </summary>
    private string NormalizeChannel(string channel)
    {
        return channel.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Map FrequencyPool entity to DTO.
    /// </summary>
    private FrequencyPoolDto MapToDto(FrequencyPool pool)
    {
        return new FrequencyPoolDto
        {
            Id = pool.Id,
            EventId = pool.EventId,
            CreatedAt = pool.CreatedAt,
            UpdatedAt = pool.UpdatedAt,
            Entries = pool.Entries
                .OrderBy(e => e.SortOrder)
                .Select(e => new FrequencyPoolEntryDto
                {
                    Id = e.Id,
                    Channel = e.Channel,
                    DisplayGroup = e.DisplayGroup,
                    SortOrder = e.SortOrder,
                    IsReserved = e.IsReserved,
                    ReservedRole = e.ReservedRole
                })
                .ToList()
        };
    }
}
