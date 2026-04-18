using MilsimPlanning.Api.Authorization;
using MilsimPlanning.Api.Data;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Domain;
using MilsimPlanning.Api.Models.Users;
using Microsoft.EntityFrameworkCore;

namespace MilsimPlanning.Api.Services;

/// <summary>
/// User management service for operations like searching users (for role assignment).
/// </summary>
public class UserService
{
    private readonly AppDbContext _db;

    public UserService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Search for users by email (case-insensitive).
    /// Used in async autocomplete for assigning roles.
    /// Returns paginated results.
    /// </summary>
    public async Task<List<UserSearchResultDto>> SearchUsersAsync(string? query, int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var searchTerm = query.Trim().ToLower();
        var users = await _db.Users
            .Where(u => u.Email.ToLower().Contains(searchTerm) ||
                        (u.UserName != null && u.UserName.ToLower().Contains(searchTerm)))
            .Take(limit)
            .Select(u => new UserSearchResultDto(
                u.Id,
                u.Email,
                u.UserName
            ))
            .ToListAsync();

        return users;
    }

    /// <summary>
    /// Get current user's event memberships (for multi-event selector).
    /// </summary>
    public async Task<List<EventMembershipDto>> GetUserEventMembershipsAsync(string userId)
    {
        var memberships = await _db.EventMemberships
            .Where(m => m.UserId == userId)
            .Include(m => m.Event)
            .OrderByDescending(m => m.JoinedAt)
            .Select(m => new EventMembershipDto(
                m.Event.Id,
                m.Event.Name,
                m.Event.StartDate,
                m.Role,
                GetRoleLabel(m.Role),
                m.JoinedAt
            ))
            .ToListAsync();

        return memberships;
    }

    /// <summary>
    /// Get display label for a role (for UI rendering).
    /// </summary>
    private static string GetRoleLabel(string roleName) => roleName switch
    {
        AppRoles.Player => "Player",
        AppRoles.SquadLeader => "Squad Leader",
        AppRoles.PlatoonLeader => "Platoon Leader",
        AppRoles.FactionCommander => "Faction Commander",
        AppRoles.EventOwner => "Event Owner",
        AppRoles.SystemAdmin => "System Admin",
        _ => "Unknown"
    };
}

/// <summary>
/// DTO for user's event membership (for multi-event selector).
/// </summary>
public record EventMembershipDto(
    Guid EventId,
    string EventName,
    DateOnly? EventDate,
    string Role,
    string RoleLabel,
    DateTime LastAccessed
);
