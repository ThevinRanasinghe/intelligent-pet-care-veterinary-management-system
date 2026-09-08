using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Configurations;

public class PetConfiguration : IEntityTypeConfiguration<Pet>
{
    public void Configure(EntityTypeBuilder<Pet> builder)
    {
        builder.ToTable("Pets");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(e => e.OwnerId)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.Species)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.Breed)
            .HasMaxLength(100);

        builder.Property(e => e.Notes)
            .HasMaxLength(2000);

        builder.Property(e => e.PhotoUrl)
            .HasMaxLength(500);

        // Relationships
        builder.HasOne(p => p.Owner)
            .WithMany(o => o.Pets)
            .HasForeignKey(p => p.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.ConsultationRequests)
            .WithOne(c => c.Pet)
            .HasForeignKey(c => c.PetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.MedicalRecords)
            .WithOne(m => m.Pet)
            .HasForeignKey(m => m.PetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.VaccinationRecords)
            .WithOne(v => v.Pet)
            .HasForeignKey(v => v.PetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
