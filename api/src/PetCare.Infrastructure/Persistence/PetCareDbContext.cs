using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PetCare.Infrastructure.Entities;

namespace PetCare.Infrastructure.Persistence;

/// <summary>
/// Main EF Core DbContext for PetCare AI.
/// Extends IdentityDbContext to include all ASP.NET Core Identity tables
/// (AspNetUsers, AspNetRoles, AspNetUserRoles, etc.).
/// </summary>
public sealed class PetCareDbContext : IdentityDbContext<ApplicationUser>
{
    public PetCareDbContext(DbContextOptions<PetCareDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Apply any entity configurations from this assembly
        builder.ApplyConfigurationsFromAssembly(typeof(PetCareDbContext).Assembly);

        // Optional: rename Identity tables to snake_case for PostgreSQL convention
        // Uncomment if desired:
        // foreach (var entity in builder.Model.GetEntityTypes())
        // {
        //     entity.SetTableName(entity.GetTableName()?.ToSnakeCase());
        // }
    }
}
