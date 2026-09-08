using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using PetCare.Domain.Constants;

namespace PetCare.Infrastructure.Seed;

/// <summary>
/// Ensures all required Identity roles exist at startup.
/// Safe to call on every startup — idempotent.
/// Does NOT use raw SQL — uses RoleManager only.
/// </summary>
public static class IdentityRoleSeeder
{
    public static async Task SeedAsync(
        RoleManager<IdentityRole> roleManager,
        ILogger logger)
    {
        foreach (var roleName in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(roleName));
                if (result.Succeeded)
                    logger.LogInformation("Role '{Role}' created.", roleName);
                else
                    logger.LogError("Failed to create role '{Role}': {Errors}",
                        roleName, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}
