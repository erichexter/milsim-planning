namespace MilsimPlanning.Api.Domain;

public static class AppRoles
{
    public const string Player           = "player";
    public const string SquadLeader      = "squad_leader";
    public const string PlatoonLeader    = "platoon_leader";
    public const string FactionCommander = "faction_commander";
    public const string EventOwner       = "event_owner";  // Role level 5: event creator/planner
    public const string SystemAdmin      = "system_admin";

    public static readonly Dictionary<string, int> Hierarchy = new()
    {
        [Player] = 1,
        [SquadLeader] = 2,
        [PlatoonLeader] = 3,
        [FactionCommander] = 4,
        [EventOwner] = 5,
        [SystemAdmin] = 6
    };
}
