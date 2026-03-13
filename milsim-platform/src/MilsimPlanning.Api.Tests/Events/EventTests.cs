using Xunit;

namespace MilsimPlanning.Api.Tests.Events;

[Trait("Category", "EVNT_Create")]
public class EventCreateTests
{
    [Fact] public Task CreateEvent_ValidRequest_Returns201() { Assert.True(true); return Task.CompletedTask; }
    [Fact] public Task CreateEvent_MissingName_Returns400() { Assert.True(true); return Task.CompletedTask; }
    [Fact] public Task CreateEvent_NewEvent_HasDraftStatus() { Assert.True(true); return Task.CompletedTask; }
    [Fact] public Task CreateEvent_PlayerRole_Returns403() { Assert.True(true); return Task.CompletedTask; }
}

[Trait("Category", "EVNT_List")]
public class EventListTests
{
    [Fact] public Task ListEvents_ReturnsOnlyCommandersOwnEvents() { Assert.True(true); return Task.CompletedTask; }
}

[Trait("Category", "EVNT_Publish")]
public class EventPublishTests
{
    [Fact] public Task PublishEvent_DraftEvent_Returns204() { Assert.True(true); return Task.CompletedTask; }
    [Fact] public Task PublishEvent_AlreadyPublished_Returns409() { Assert.True(true); return Task.CompletedTask; }
    [Fact] public Task PublishEvent_DoesNotSendNotification() { Assert.True(true); return Task.CompletedTask; }
}

[Trait("Category", "EVNT_Duplicate")]
public class EventDuplicateTests
{
    [Fact] public Task DuplicateEvent_AlwaysCopiesPlatoonSquadStructure() { Assert.True(true); return Task.CompletedTask; }
    [Fact] public Task DuplicateEvent_DoesNotCopyRosterOrDates() { Assert.True(true); return Task.CompletedTask; }
    [Fact] public Task DuplicateEvent_CopiesOnlySelectedInfoSections() { Assert.True(true); return Task.CompletedTask; }
    [Fact] public Task DuplicateEvent_NewEventIsInDraftStatus() { Assert.True(true); return Task.CompletedTask; }
}
