using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PetCare.Domain.Constants;
using PetCare.Infrastructure.Entities;

namespace PetCare.Infrastructure.Seed;

/// <summary>
/// Bootstrap mechanism for creating the first SuperAdmin account in development.
/// Reads credentials from:
///   BootstrapAdmin:Email
///   BootstrapAdmin:Password
/// Never hardcodes credentials. If the SuperAdmin already exists, does nothing.
/// </summary>
public static class SuperAdminSeeder
{
    public static async Task SeedAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration               configuration,
        ILogger                      logger)
    {
        var email    = configuration["BootstrapAdmin:Email"];
        var password = configuration["BootstrapAdmin:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "SuperAdmin bootstrap skipped: BootstrapAdmin:Email or BootstrapAdmin:Password not configured.");
            return;
        }

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            logger.LogInformation("SuperAdmin '{Email}' already exists — skipping seed.", email);
            return;
        }

        var admin = new ApplicationUser
        {
            UserName  = email,
            Email     = email,
            FirstName = "Super",
            LastName  = "Admin",
            IsActive  = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var result = await userManager.CreateAsync(admin, password);
        if (!result.Succeeded)
        {
            logger.LogError("Failed to create SuperAdmin: {Errors}",
                string.Join(", ", result.Errors.Select(e => e.Description)));
            return;
        }

        var roleResult = await userManager.AddToRoleAsync(admin, Roles.SuperAdmin);
        if (roleResult.Succeeded)
            logger.LogInformation("SuperAdmin '{Email}' created successfully.", email);
        else
            logger.LogError("SuperAdmin created but role assignment failed: {Errors}",
                string.Join(", ", roleResult.Errors.Select(e => e.Description)));
    }
}
