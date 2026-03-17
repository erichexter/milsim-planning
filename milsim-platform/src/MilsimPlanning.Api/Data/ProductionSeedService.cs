using Microsoft.AspNetCore.Identity;
using MilsimPlanning.Api.Data.Entities;

namespace MilsimPlanning.Api.Data;

/// <summary>
/// Seeds roles and an initial faction_commander account on first production startup.
///
/// Reads credentials from environment variables:
///   Seed__AdminEmail     — email address for the initial commander account
///   Seed__AdminPassword  — password for the initial commander account
///
/// Entirely skipped if any user already exists (idempotent).
/// </summary>
public static class ProductionSeedService
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // ── Ensure all roles exist (required in every environment) ────────────
        string[] roles = ["player", "squad_leader", "platoon_leader", "faction_commander", "system_admin"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // ── Skip if any user already exists ───────────────────────────────────
        if (userManager.Users.Any()) return;

        var email    = config["Seed__AdminEmail"];
        var password = config["Seed__AdminPassword"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            // No credentials configured — skip silently. Admin must be created manually.
            return;
        }

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Production seed failed for {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        await userManager.AddToRoleAsync(user, "faction_commander");

        user.Profile = new UserProfile
        {
            UserId = user.Id,
            Callsign = "ACTUAL",
            DisplayName = "Commander",
            User = user
        };

        await db.SaveChangesAsync();
    }
}
