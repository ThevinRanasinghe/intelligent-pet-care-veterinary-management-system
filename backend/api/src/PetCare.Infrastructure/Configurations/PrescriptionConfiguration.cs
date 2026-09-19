using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Configurations;

public class PrescriptionConfiguration : IEntityTypeConfiguration<Prescription>
{
    public void Configure(EntityTypeBuilder<Prescription> builder)
    {
        builder.ToTable("Prescriptions");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(p => p.TreatmentRecordId)
            .IsRequired();

        builder.Property(p => p.MedicineId)
            .IsRequired();

        builder.Property(p => p.Dosage)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.DurationDays)
            .IsRequired();

        builder.Property(p => p.CreatedAt);

        // Prescription -> TreatmentRecord (many-to-1)
        builder.HasOne(p => p.TreatmentRecord)
            .WithMany(t => t.Prescriptions)
            .HasForeignKey(p => p.TreatmentRecordId)
            .OnDelete(DeleteBehavior.Cascade);

        // Prescription -> Medicine (many-to-1)
        builder.HasOne(p => p.Medicine)
            .WithMany()
            .HasForeignKey(p => p.MedicineId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
