using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Configurations;

public class ConsultationStatusHistoryConfiguration : IEntityTypeConfiguration<ConsultationStatusHistory>
{
    public void Configure(EntityTypeBuilder<ConsultationStatusHistory> builder)
    {
        builder.ToTable("ConsultationStatusHistories");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.ConsultationRequestId)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Comments)
            .HasMaxLength(1000);

        builder.Property(x => x.ChangedAt);

        builder.HasOne(x => x.ConsultationRequest)
            .WithMany(x => x.StatusHistories)
            .HasForeignKey(x => x.ConsultationRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
