using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
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
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
                         .CreateLogger("ProductionSeedService");

        // ── Ensure all roles exist (required in every environment) ────────────
        foreach (var role in AppRoles.AllRoles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
                logger.LogInformation("Production seed: created role '{Role}'", role);
            }
        }

        // ── Skip if any user already exists ───────────────────────────────────
        var userCount = userManager.Users.Count();
        logger.LogInformation("Production seed: {Count} user(s) already exist", userCount);
        if (userCount > 0) return;

        var email    = config["Seed:AdminEmail"];
        var password = config["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Production seed: Seed__AdminEmail / Seed__AdminPassword not set — skipping user creation");
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
        logger.LogInformation("Production seed: created initial commander account '{Email}'", email);
    }
}
