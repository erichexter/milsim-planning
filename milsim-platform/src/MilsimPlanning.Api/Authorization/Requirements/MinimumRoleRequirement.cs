using Microsoft.AspNetCore.Authorization;

namespace MilsimPlanning.Api.Authorization.Requirements;

public record MinimumRoleRequirement(string MinimumRole) : IAuthorizationRequirement;
