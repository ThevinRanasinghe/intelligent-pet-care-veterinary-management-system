using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PetCare.Domain.Entities;
using PetCare.Infrastructure.Entities;

namespace PetCare.Infrastructure.Persistence;

/// <summary>
/// Main EF Core DbContext for Beacon Pet Health.
/// Extends IdentityDbContext to include all ASP.NET Core Identity tables
/// plus multi-organization and application entities.
/// </summary>
public sealed class PetCareDbContext : IdentityDbContext<ApplicationUser>
{
    public DbSet<Organization> Organizations => Set<Organization>();

    public PetCareDbContext(DbContextOptions<PetCareDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Apply any entity configurations from this assembly
        builder.ApplyConfigurationsFromAssembly(typeof(PetCareDbContext).Assembly);

        // ── Organization configuration ──
        builder.Entity<Organization>(entity =>
        {
            entity.ToTable("Organizations");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.RegistrationNumber)
                .HasMaxLength(100);

            entity.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(e => e.Phone)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.Address)
                .IsRequired()
                .HasMaxLength(300);

            entity.Property(e => e.City)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Country)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Status)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.HasIndex(e => e.Email);
            entity.HasIndex(e => e.Name);
        });

        // ── ApplicationUser - Organization relationship ──
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.HasOne(u => u.Organization)
                .WithMany()
                .HasForeignKey(u => u.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
