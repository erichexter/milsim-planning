namespace MilsimPlanning.Api.Models.Events;

// CRITICAL: CopyInfoSectionIds must be present even though Phase 2 has no info sections.
// Forward compatibility for Phase 3. API stores/accepts this list.
public record DuplicateEventRequest(
    Guid[] CopyInfoSectionIds  // empty array when no sections exist; never null
);
