using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Domain;

namespace MilsimPlanning.Api.Services;

/// <summary>
/// Represents the authenticated user for the current HTTP request.
/// All properties are read from JWT claims; EventMembershipIds is
/// lazily loaded from the database on first access and cached for
/// the lifetime of the scoped service (one HTTP request).
/// </summary>
public interface ICurrentUser
{
    string UserId { get; }
    string Role { get; }

    /// <summary>
    /// Set of event IDs the user is a member of.
    /// Loaded with a single DB query on first access; subsequent accesses
    /// return the cached set (no additional DB queries per request).
    /// </summary>
    IReadOnlySet<Guid> EventMembershipIds { get; }
}

/// <summary>
/// Scoped per-request implementation of ICurrentUser.
/// Requires IHttpContextAccessor (registered in Program.cs).
/// </summary>
public class CurrentUserService : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AppDbContext _db;

    // Cached per request — loaded on first access of EventMembershipIds
    private IReadOnlySet<Guid>? _cachedEventIds;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor, AppDbContext db)
    {
        _httpContextAccessor = httpContextAccessor;
        _db = db;
    }

    public string UserId =>
        _httpContextAccessor.HttpContext!.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? _httpContextAccessor.HttpContext!.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("User is not authenticated (no sub claim)");

    public string Role =>
        _httpContextAccessor.HttpContext!.User.FindFirstValue(ClaimTypes.Role)
        ?? AppRoles.Player;

    public IReadOnlySet<Guid> EventMembershipIds =>
        _cachedEventIds ??= LoadEventIds();

    private HashSet<Guid> LoadEventIds()
    {
        // Single DB query, result cached for the request lifetime
        return _db.EventMemberships
            .Where(m => m.UserId == UserId)
            .Select(m => m.EventId)
            .ToHashSet();
    }
}
