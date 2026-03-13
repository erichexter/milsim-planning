namespace MilsimPlanning.Api.Models.Hierarchy;

public record PlayerDto(Guid Id, string Name, string? Callsign, string? TeamAffiliation);
public record SquadDto(Guid Id, string Name, List<PlayerDto> Players);
public record PlatoonDto(Guid Id, string Name, List<SquadDto> Squads);

public record RosterHierarchyDto(
    List<PlatoonDto> Platoons,
    List<PlayerDto> UnassignedPlayers
);
