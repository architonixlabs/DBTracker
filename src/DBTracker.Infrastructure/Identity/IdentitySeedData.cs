using Microsoft.AspNetCore.Identity;

namespace DBTracker.Infrastructure.Identity;

/// <summary>Dev/Testing-only seed accounts with fixed demo passwords. Replace them before deploying anywhere else.</summary>
public static class IdentitySeedData
{
    private const string DemoPassword = "ArxDemo#2026!";

    public static async Task SeedAsync(RoleManager<IdentityRole> roleManager, UserManager<ApplicationUser> userManager)
    {
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        await EnsureUserAsync(userManager, "admin@architonixlabs.com", "Platform Admin", Roles.Admin);
        await EnsureUserAsync(userManager, "priya.nair@architonixlabs.com", "Priya Nair (DBA)", Roles.Dba);
        await EnsureUserAsync(userManager, "marcus.chen@architonixlabs.com", "Marcus Chen (IT Manager)", Roles.ItManager);
        await EnsureUserAsync(userManager, "angela.price@contoso.com", "Angela Price (Auditor)", Roles.Auditor);
        await EnsureUserAsync(userManager, "dev.patel@ediqra.com", "Dev Patel (Viewer)", Roles.Viewer);
    }

    private static async Task EnsureUserAsync(UserManager<ApplicationUser> userManager, string email, string displayName, string role)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            return;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = displayName,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, DemoPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(user, role);
        }
    }
}
