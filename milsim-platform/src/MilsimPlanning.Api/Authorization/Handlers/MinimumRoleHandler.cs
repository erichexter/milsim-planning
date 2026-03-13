using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using MilsimPlanning.Api.Authorization.Requirements;
using MilsimPlanning.Api.Domain;

namespace MilsimPlanning.Api.Authorization.Handlers;

/// <summary>
/// Single source of truth for all role hierarchy checks.
/// Reads ClaimTypes.Role from the authenticated user and compares
/// its numeric level against the requirement's minimum level using
/// AppRoles.Hierarchy — no raw role string comparisons anywhere else.
/// </summary>
public class MinimumRoleHandler : AuthorizationHandler<MinimumRoleRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MinimumRoleRequirement requirement)
    {
        var userRole = context.User.FindFirstValue(ClaimTypes.Role);
        if (userRole is null) return Task.CompletedTask; // unauthenticated → fail silently

        var userLevel = AppRoles.Hierarchy.GetValueOrDefault(userRole, 0);
        var minLevel  = AppRoles.Hierarchy.GetValueOrDefault(requirement.MinimumRole, 99);

        if (userLevel >= minLevel)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
