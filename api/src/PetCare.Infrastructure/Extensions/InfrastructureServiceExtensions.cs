using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PetCare.Application.Interfaces;
using PetCare.Infrastructure.Entities;
using PetCare.Infrastructure.Persistence;
using PetCare.Infrastructure.Services;

namespace PetCare.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering Infrastructure-layer services.
/// Call from Program.cs or an Api-layer extension.
/// </summary>
public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── PostgreSQL / EF Core ──────────────────────────────────────────────
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is not configured.");

        services.AddDbContext<PetCareDbContext>(options =>
            options.UseNpgsql(connectionString));

        // ── ASP.NET Core Identity ─────────────────────────────────────────────
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                // Password policy (must stay in sync with the Registration validator)
                options.Password.RequireDigit           = true;
                options.Password.RequiredLength         = 8;
                options.Password.RequireUppercase       = true;
                options.Password.RequireLowercase       = true;
                options.Password.RequireNonAlphanumeric = true;

                // Lockout
                options.Lockout.DefaultLockoutTimeSpan  = TimeSpan.FromMinutes(15);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers      = true;

                // User
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<PetCareDbContext>()
            .AddDefaultTokenProviders();

        // ── Application Services ──────────────────────────────────────────────
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IAuthService,  AuthService>();

        return services;
    }
}
