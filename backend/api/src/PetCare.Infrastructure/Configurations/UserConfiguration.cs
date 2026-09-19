using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Configurations;

/// <summary>
/// Shared authentication User table. No HasData here: unlike Veterinarian
/// (a static reference table), User rows carry a password hash that must
/// be generated through IPasswordHasher, so a development account is
/// created at runtime by DevelopmentSeeder instead of via migration
/// HasData (mirrors how Appointment/Quotation/Approval dev fixtures are
/// seeded).
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.Property(u => u.PasswordHash)
            .IsRequired();

        builder.Property(u => u.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(u => u.Role)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(u => u.Active)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(u => u.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(u => u.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");
    }
}
