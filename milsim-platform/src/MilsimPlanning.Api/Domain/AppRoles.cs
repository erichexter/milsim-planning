namespace MilsimPlanning.Api.Domain;

public static class AppRoles
{
    public const string Player           = "player";
    public const string SquadLeader      = "squad_leader";
    public const string PlatoonLeader    = "platoon_leader";
    public const string FactionCommander = "faction_commander";
    public const string SystemAdmin      = "system_admin";

    // Phase 5 (Briefing Board): dedicated role for briefing administration
    public const string BriefingAdmin    = "briefing_admin";

    /// <summary>All roles that must exist in the database. Used by seed services to avoid duplication.</summary>
    public static readonly string[] AllRoles =
    [
        Player, SquadLeader, PlatoonLeader, FactionCommander, SystemAdmin, BriefingAdmin
    ];

    public static readonly Dictionary<string, int> Hierarchy = new()
    {
        [Player] = 1,
        [SquadLeader] = 2,
        [PlatoonLeader] = 3,
        [FactionCommander] = 4,
        [BriefingAdmin] = 4,   // same level as FactionCommander
        [SystemAdmin] = 5
    };
}
