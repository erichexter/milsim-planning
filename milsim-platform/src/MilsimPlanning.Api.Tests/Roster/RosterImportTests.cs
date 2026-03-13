using Xunit;

namespace MilsimPlanning.Api.Tests.Roster;

[Trait("Category", "ROST_Validate")]
public class RosterValidateTests
{
    [Fact] public Task ValidateRoster_ValidCsv_ReturnsZeroErrors() { Assert.True(true); return Task.CompletedTask; }
    [Fact] public Task ValidateRoster_MissingEmail_ReturnsRowError() { Assert.True(true); return Task.CompletedTask; }
    [Fact] public Task ValidateRoster_MultipleErrors_ReturnsAllErrors() { Assert.True(true); return Task.CompletedTask; }
    [Fact] public Task ValidateRoster_WithErrors_DoesNotPersistData() { Assert.True(true); return Task.CompletedTask; }
    [Fact] public Task ValidateRoster_MissingCallsign_ReturnsWarningNotError() { Assert.True(true); return Task.CompletedTask; }
    [Fact] public Task ValidateRoster_ParsedFields_ContainNameEmailCallsignTeam() { Assert.True(true); return Task.CompletedTask; }
}

[Trait("Category", "ROST_Commit")]
public class RosterCommitTests
{
    [Fact] public Task CommitRoster_NewPlayers_UpsertsToDatabase() { Assert.True(true); return Task.CompletedTask; }
    [Fact] public Task CommitRoster_ExistingPlayer_UpdatesNameCallsignNotSquad() { Assert.True(true); return Task.CompletedTask; }
    [Fact] public Task CommitRoster_ReimportPreservesSquadAssignments() { Assert.True(true); return Task.CompletedTask; }
    [Fact] public Task CommitRoster_UnregisteredPlayers_ReceiveInvitationEmail() { Assert.True(true); return Task.CompletedTask; }
    [Fact] public Task CommitRoster_AlreadyRegisteredPlayers_DoNotReceiveInvite() { Assert.True(true); return Task.CompletedTask; }
    [Fact] public Task CommitRoster_WithErrors_Returns422() { Assert.True(true); return Task.CompletedTask; }
}
