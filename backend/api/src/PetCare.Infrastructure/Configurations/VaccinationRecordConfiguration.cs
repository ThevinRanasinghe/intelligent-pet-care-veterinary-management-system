using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Configurations;

public class VaccinationRecordConfiguration : IEntityTypeConfiguration<VaccinationRecord>
{
    public void Configure(EntityTypeBuilder<VaccinationRecord> builder)
    {
        builder.ToTable("VaccinationRecords");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(e => e.PetId)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(e => e.VaccineName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(e => e.VeterinarianName)
            .HasMaxLength(100);

        builder.Property(e => e.BatchNumber)
            .HasMaxLength(50);
    }
}
