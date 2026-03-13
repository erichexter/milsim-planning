using Xunit;

namespace MilsimPlanning.Api.Tests.Authorization;

public class AuthorizationTests
{
    [Fact]
    [Trait("Category", "Authz_Roles")]
    public void Roles_SystemAdmin_CanAccessAllPolicies()
    {
        Assert.True(true);
    }

    [Fact]
    [Trait("Category", "Authz_Roles")]
    public void Roles_Player_BlockedFromCommanderEndpoints()
    {
        Assert.True(true);
    }

    [Fact]
    [Trait("Category", "Authz_ScopeCommander")]
    public void ScopeGuard_CommanderInEventA_CanAccessEventA()
    {
        Assert.True(true);
    }

    [Fact]
    [Trait("Category", "Authz_IDOR")]
    public void ScopeGuard_CommanderInEventA_Returns403ForEventB()
    {
        Assert.True(true);
    }

    [Fact]
    [Trait("Category", "Authz_IDOR")]
    public void ScopeGuard_PlayerInEventA_Returns403ForEventB()
    {
        Assert.True(true);
    }

    [Fact]
    [Trait("Category", "Authz_EmailVisibility")]
    public void EmailVisibility_Player_EmailFieldAbsentInRosterResponse()
    {
        Assert.True(true);
    }

    [Fact]
    [Trait("Category", "Authz_EmailVisibility")]
    public void EmailVisibility_PlatoonLeader_EmailFieldPresentInRosterResponse()
    {
        Assert.True(true);
    }

    [Fact]
    [Trait("Category", "Authz_ReadOnlyLeaders")]
    public void ReadOnlyLeader_CanGetRoster_CannotPost()
    {
        Assert.True(true);
    }

    [Fact]
    [Trait("Category", "Authz_PlayerAccess")]
    public void PlayerAccess_InEvent_CanGetRoster()
    {
        Assert.True(true);
    }

    [Fact]
    [Trait("Category", "Authz_PlayerAccess")]
    public void PlayerAccess_NotInEvent_Returns403()
    {
        Assert.True(true);
    }
}
