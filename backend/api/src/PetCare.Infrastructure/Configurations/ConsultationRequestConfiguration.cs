using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Configurations;

public class ConsultationRequestConfiguration : IEntityTypeConfiguration<ConsultationRequest>
{
    public void Configure(EntityTypeBuilder<ConsultationRequest> builder)
    {
        builder.ToTable("ConsultationRequests");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(e => e.PetId)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(e => e.OwnerId)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(e => e.SymptomsDescription)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(e => e.PhotoUrl)
            .HasMaxLength(500);

        builder.Property(e => e.BudgetLimit)
            .HasPrecision(18, 2);

        builder.Property(e => e.PreferredBranch)
            .HasMaxLength(100);

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.StatusNotes)
            .HasMaxLength(1000);

        // Relationships
        builder.HasOne(c => c.Pet)
            .WithMany(p => p.ConsultationRequests)
            .HasForeignKey(c => c.PetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Owner)
            .WithMany(o => o.ConsultationRequests)
            .HasForeignKey(c => c.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.StatusHistories)
            .WithOne(h => h.ConsultationRequest)
            .HasForeignKey(h => h.ConsultationRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
