using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Configurations;

public class MedicalRecordConfiguration : IEntityTypeConfiguration<MedicalRecord>
{
    public void Configure(EntityTypeBuilder<MedicalRecord> builder)
    {
        builder.ToTable("MedicalRecords");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(e => e.PetId)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(e => e.Diagnosis)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(e => e.Treatment)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(e => e.VeterinarianName)
            .HasMaxLength(100);

        builder.Property(e => e.ClinicalNotes)
            .HasMaxLength(3000);
    }
}
