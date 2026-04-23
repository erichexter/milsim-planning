namespace MilsimPlanning.Api.Models.CheckIn;

public record CheckInRecordDto(
    Guid ParticipantId,
    string Name,
    string Faction,
    DateTime ScannedAtUtc
);
