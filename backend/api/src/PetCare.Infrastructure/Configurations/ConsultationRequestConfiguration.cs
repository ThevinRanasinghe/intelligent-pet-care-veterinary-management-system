using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Configurations;

public class ConsultationRequestConfiguration : IEntityTypeConfiguration<ConsultationRequest>
{
    public void Configure(EntityTypeBuilder<ConsultationRequest> builder)
    {
        builder.ToTable("ConsultationRequests");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.PetId)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.OwnerId)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.SymptomsDescription)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(x => x.PhotoUrl)
            .HasMaxLength(500);

        builder.Property(x => x.PreferredDate)
            .IsRequired();

        builder.Property(x => x.BudgetLimit)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.PreferredClinicLocationLat);

        builder.Property(x => x.PreferredClinicLocationLong);

        builder.Property(x => x.PreferredBranch)
            .HasMaxLength(100);

        builder.Property(x => x.Status)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.StatusNotes)
            .HasMaxLength(1000);

        builder.Property(x => x.CreatedAt);

        builder.Property(x => x.UpdatedAt);

        builder.HasOne(x => x.Pet)
            .WithMany(x => x.ConsultationRequests)
            .HasForeignKey(x => x.PetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Owner)
            .WithMany(x => x.ConsultationRequests)
            .HasForeignKey(x => x.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.StatusHistories)
            .WithOne(x => x.ConsultationRequest)
            .HasForeignKey(x => x.ConsultationRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
