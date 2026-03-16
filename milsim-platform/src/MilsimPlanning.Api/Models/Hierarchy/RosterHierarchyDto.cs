namespace MilsimPlanning.Api.Models.Hierarchy;

public record PlayerDto(Guid Id, string Name, string? Callsign, string? TeamAffiliation, string? Role);
public record SquadDto(Guid Id, string Name, List<PlayerDto> Players);
public record PlatoonDto(Guid Id, string Name, List<SquadDto> Squads);

public record RosterHierarchyDto(
    List<PlatoonDto> Platoons,
    List<PlayerDto> UnassignedPlayers
);
