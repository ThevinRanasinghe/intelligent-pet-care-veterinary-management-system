using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;
using PetCare.Infrastructure.Seed;

namespace PetCare.Infrastructure.Configurations;

public class VeterinarianConfiguration : IEntityTypeConfiguration<Veterinarian>
{
    public void Configure(EntityTypeBuilder<Veterinarian> builder)
    {
        builder.ToTable("Veterinarians");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(v => v.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(v => v.Specialisation)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(v => v.Branch)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(v => v.Active)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(v => v.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(v => v.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.HasMany(v => v.AppointmentSlots)
            .WithOne(s => s.Veterinarian)
            .HasForeignKey(s => s.VeterinarianId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(v => v.Appointments)
            .WithOne(a => a.Veterinarian)
            .HasForeignKey(a => a.VeterinarianId)
            .OnDelete(DeleteBehavior.Restrict);

        // Development/reference seed data (fixed GUIDs for reproducibility).
        // See docs/database/scheduling-billing-approval-domain-model.md#seed-data.
        var seedCreatedAt = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

        builder.HasData(
            new
            {
                Id = SeedIds.VetAnikaPerera,
                Name = "Dr. Anika Perera",
                Specialisation = "Small Animal Medicine",
                Branch = "Colombo",
                Active = true,
                CreatedAt = seedCreatedAt,
                UpdatedAt = seedCreatedAt
            },
            new
            {
                Id = SeedIds.VetRohanFernando,
                Name = "Dr. Rohan Fernando",
                Specialisation = "Surgery",
                Branch = "Kandy",
                Active = true,
                CreatedAt = seedCreatedAt,
                UpdatedAt = seedCreatedAt
            },
            new
            {
                Id = SeedIds.VetNadeeSilva,
                Name = "Dr. Nadee Silva",
                Specialisation = "Dermatology",
                Branch = "Galle",
                Active = true,
                CreatedAt = seedCreatedAt,
                UpdatedAt = seedCreatedAt
            }
        );
    }
}
