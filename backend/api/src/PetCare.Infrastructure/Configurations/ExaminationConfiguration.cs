using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Configurations;

public class ExaminationConfiguration : IEntityTypeConfiguration<Examination>
{
    public void Configure(EntityTypeBuilder<Examination> builder)
    {
        builder.ToTable("Examinations");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(e => e.PetId)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(e => e.VeterinarianId)
            .IsRequired();

        builder.Property(e => e.ConsultationRequestId)
            .HasMaxLength(30);

        builder.Property(e => e.Symptoms)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(e => e.Notes)
            .HasMaxLength(1000);

        builder.Property(e => e.ExaminationDate)
            .IsRequired();

        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.UpdatedAt);

        // Examination -> Veterinarian (many-to-1); carries org scope.
        builder.HasOne(e => e.Veterinarian)
            .WithMany()
            .HasForeignKey(e => e.VeterinarianId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.VeterinarianId)
            .HasDatabaseName("IX_Examinations_VeterinarianId");

        // Examination -> Pet (many-to-1)
        builder.HasOne(e => e.Pet)
            .WithMany()
            .HasForeignKey(e => e.PetId)
            .OnDelete(DeleteBehavior.Restrict);

        // Examination -> ConsultationRequest (many-to-1, optional)
        builder.HasOne(e => e.ConsultationRequest)
            .WithMany()
            .HasForeignKey(e => e.ConsultationRequestId)
            .OnDelete(DeleteBehavior.SetNull);

        // Examination -> Diagnosis (1-to-1, Diagnosis has FK)
        builder.HasOne(e => e.Diagnosis)
            .WithOne(d => d.Examination)
            .HasForeignKey<Diagnosis>(d => d.ExaminationId);
    }
}
