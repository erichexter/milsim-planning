using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MilsimPlanning.Api.Data.Entities;
using MilsimPlanning.Api.Domain;

namespace MilsimPlanning.Api.Data;

/// <summary>
/// Seeds deterministic dev accounts on startup (Development environment only).
///
/// Accounts created:
///   commander@dev.local  /  DevPass123!   role: faction_commander
///   player@dev.local     /  DevPass123!   role: player
///
/// Idempotent — safe to call every startup; skips users that already exist.
/// </summary>
public static class DevSeedService
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // ── Ensure all roles exist ────────────────────────────────────────────
        string[] roles = ["player", "squad_leader", "platoon_leader", "faction_commander", "system_admin"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // ── Seed users ────────────────────────────────────────────────────────
        await EnsureUserAsync(
            userManager, db,
            email: "commander@dev.local",
            password: "DevPass123!",
            callsign: "ACTUAL",
            displayName: "Dev Commander",
            role: "faction_commander"
        );

        await EnsureUserAsync(
            userManager, db,
            email: "player@dev.local",
            password: "DevPass123!",
            callsign: "BRAVO-1",
            displayName: "Dev Player",
            role: "player"
        );

        // ── Backfill missing EventMembership rows for commanders ──────────────
        // Fixes events created before the auto-enroll logic was added to CreateEventAsync.
        await BackfillCommanderMembershipsAsync(db);

        // ── Backfill missing EventMembership rows for roster-imported players ─
        // Links EventPlayer.UserId and creates EventMembership for any player
        // whose email matches an existing AppUser but was imported before this fix.
        await BackfillPlayerMembershipsAsync(userManager, db);
    }

    /// <summary>
    /// Idempotent backfill: for every event whose faction commander has no
    /// EventMembership row, insert one. Safe to run on every startup.
    /// </summary>
    private static async Task BackfillCommanderMembershipsAsync(AppDbContext db)
    {
        var eventsWithoutMembership = await db.Events
            .Include(e => e.Faction)
            .Where(e => e.Faction != null &&
                        !db.EventMemberships.Any(m =>
                            m.EventId == e.Id && m.UserId == e.Faction.CommanderId))
            .ToListAsync();

        foreach (var evt in eventsWithoutMembership)
        {
            db.EventMemberships.Add(new EventMembership
            {
                UserId = evt.Faction.CommanderId,
                EventId = evt.Id,
                Role = AppRoles.FactionCommander,
                JoinedAt = DateTime.UtcNow,
            });
        }

        if (eventsWithoutMembership.Count > 0)
            await db.SaveChangesAsync();
    }

    /// <summary>
    /// Idempotent backfill: for every EventPlayer whose email matches an AppUser,
    /// set UserId and create an EventMembership (player role) if not already present.
    /// Safe to run on every startup.
    /// </summary>
    private static async Task BackfillPlayerMembershipsAsync(UserManager<AppUser> userManager, AppDbContext db)
    {
        // Find all EventPlayer rows that are either not yet linked or have no membership
        var unlinkedPlayers = await db.EventPlayers
            .Where(ep => ep.UserId == null || !db.EventMemberships.Any(m => m.EventId == ep.EventId && m.UserId == ep.UserId))
            .ToListAsync();

        var changed = false;
        foreach (var ep in unlinkedPlayers)
        {
            var appUser = await userManager.FindByEmailAsync(ep.Email);
            if (appUser is null) continue;

            // Link UserId
            if (ep.UserId is null)
            {
                ep.UserId = appUser.Id;
                changed = true;
            }

            // Upsert membership
            var alreadyMember = await db.EventMemberships
                .AnyAsync(m => m.EventId == ep.EventId && m.UserId == appUser.Id);
            if (!alreadyMember)
            {
                db.EventMemberships.Add(new EventMembership
                {
                    UserId = appUser.Id,
                    EventId = ep.EventId,
                    Role = AppRoles.Player,
                    JoinedAt = DateTime.UtcNow,
                });
                changed = true;
            }
        }

        if (changed)
            await db.SaveChangesAsync();
    }

    private static async Task EnsureUserAsync(
        UserManager<AppUser> userManager,
        AppDbContext db,
        string email,
        string password,
        string callsign,
        string displayName,
        string role)
    {
        if (await userManager.FindByEmailAsync(email) is not null)
            return; // already seeded

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Dev seed failed for {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        await userManager.AddToRoleAsync(user, role);

        user.Profile = new UserProfile
        {
            UserId = user.Id,
            Callsign = callsign,
            DisplayName = displayName,
            User = user
        };
        await db.SaveChangesAsync();
    }
}
