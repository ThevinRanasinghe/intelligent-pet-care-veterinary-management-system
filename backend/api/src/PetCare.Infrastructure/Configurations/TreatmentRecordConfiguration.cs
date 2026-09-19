using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Configurations;

public class TreatmentRecordConfiguration : IEntityTypeConfiguration<TreatmentRecord>
{
    public void Configure(EntityTypeBuilder<TreatmentRecord> builder)
    {
        builder.ToTable("TreatmentRecords");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(t => t.DiagnosisId)
            .IsRequired();

        builder.Property(t => t.ProcedureName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.Notes)
            .HasMaxLength(1000);

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.CreatedAt);
        builder.Property(t => t.UpdatedAt);

        // TreatmentRecord -> Diagnosis (many-to-1)
        builder.HasOne(t => t.Diagnosis)
            .WithMany(d => d.TreatmentRecords)
            .HasForeignKey(t => t.DiagnosisId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
