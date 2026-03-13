using MilsimPlanning.Api.Domain;
using MilsimPlanning.Api.Services;

namespace MilsimPlanning.Api.Authorization;

/// <summary>
/// IDOR prevention pattern — call AssertEventAccess at the top of every
/// service method that accepts an eventId parameter.
///
/// SystemAdmin bypasses the scope check (unrestricted access).
/// All other roles must have an active EventMembership for the requested event.
/// </summary>
public static class ScopeGuard
{
    /// <summary>
    /// Throws <see cref="ForbiddenException"/> if the current user is not a member
    /// of the specified event. SystemAdmin is always permitted.
    /// </summary>
    public static void AssertEventAccess(ICurrentUser currentUser, Guid eventId)
    {
        if (currentUser.Role == AppRoles.SystemAdmin) return; // admins bypass scope guard

        if (!currentUser.EventMembershipIds.Contains(eventId))
            throw new ForbiddenException($"User does not have access to event {eventId}");
    }
}
