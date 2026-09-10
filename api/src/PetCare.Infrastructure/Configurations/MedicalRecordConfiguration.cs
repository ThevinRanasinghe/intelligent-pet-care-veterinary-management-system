using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Configurations;

public class MedicalRecordConfiguration : IEntityTypeConfiguration<MedicalRecord>
{
    public void Configure(EntityTypeBuilder<MedicalRecord> builder)
    {
        builder.ToTable("MedicalRecords");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .HasMaxLength(30);

        builder.Property(m => m.PetId)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(m => m.Diagnosis)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(m => m.Treatment)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(m => m.VeterinarianName)
            .HasMaxLength(100);

        builder.Property(m => m.ClinicalNotes)
            .HasMaxLength(3000);

        builder.Property(m => m.RecordDate)
            .IsRequired();

        builder.Property(m => m.CreatedAt)
            .IsRequired();

        builder.HasIndex(m => m.PetId);

        builder.HasOne(m => m.Pet)
            .WithMany(p => p.MedicalRecords)
            .HasForeignKey(m => m.PetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
