namespace MilsimPlanning.Api.Models.Channels;

public record ConflictSummaryDto(
    Guid OperationId,
    int ConflictCount,
    List<ConflictItemDto> Conflicts
);
