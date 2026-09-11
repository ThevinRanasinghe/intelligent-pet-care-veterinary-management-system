using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;

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

        builder.HasIndex(v => v.OrganizationId);

        builder.HasOne(v => v.Organization)
            .WithMany()
            .HasForeignKey(v => v.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(v => v.AppointmentSlots)
            .WithOne(s => s.Veterinarian)
            .HasForeignKey(s => s.VeterinarianId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(v => v.Appointments)
            .WithOne(a => a.Veterinarian)
            .HasForeignKey(a => a.VeterinarianId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
