using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Configurations;

public class ConsultationStatusHistoryConfiguration : IEntityTypeConfiguration<ConsultationStatusHistory>
{
    public void Configure(EntityTypeBuilder<ConsultationStatusHistory> builder)
    {
        builder.ToTable("ConsultationStatusHistories");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();

        builder.Property(e => e.ConsultationRequestId)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Comments)
            .HasMaxLength(1000);
    }
}
