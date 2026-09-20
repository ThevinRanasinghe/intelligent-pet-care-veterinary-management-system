using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Configurations;

/// <summary>
/// EF Core configuration for the Organization table. Each veterinary
/// organization is a tenant that owns its staff and inventory.
/// </summary>
public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("Organizations");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(o => o.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(o => o.Name)
            .IsUnique();

        builder.Property(o => o.RegistrationNumber)
            .HasMaxLength(100);

        builder.Property(o => o.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(o => o.Email)
            .IsUnique();

        builder.Property(o => o.Phone)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(o => o.Address)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(o => o.City)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(o => o.Country)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(o => o.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(o => o.IsActive)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(o => o.RejectionReason)
            .HasMaxLength(500);

        builder.Property(o => o.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(o => o.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.HasMany(o => o.Users)
            .WithOne(u => u.Organization)
            .HasForeignKey(u => u.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
