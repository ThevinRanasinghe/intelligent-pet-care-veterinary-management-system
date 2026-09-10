using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Configurations;

public class ConsultationRequestConfiguration : IEntityTypeConfiguration<ConsultationRequest>
{
    public void Configure(EntityTypeBuilder<ConsultationRequest> builder)
    {
        builder.ToTable("ConsultationRequests");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .HasMaxLength(30);

        builder.Property(r => r.PetId)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(r => r.OwnerId)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(r => r.SymptomsDescription)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(r => r.PhotoUrl)
            .HasMaxLength(500);

        builder.Property(r => r.PreferredDate)
            .IsRequired();

        builder.Property(r => r.BudgetLimit)
            .IsRequired()
            .HasColumnType("numeric(18,2)");

        builder.Property(r => r.PreferredBranch)
            .HasMaxLength(100);

        builder.Property(r => r.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(r => r.StatusNotes)
            .HasMaxLength(1000);

        builder.Property(r => r.CreatedAt)
            .IsRequired();

        builder.Property(r => r.UpdatedAt)
            .IsRequired();

        builder.HasIndex(r => r.OrganizationId);
        builder.HasIndex(r => r.PetId);
        builder.HasIndex(r => r.OwnerId);

        builder.HasOne(r => r.Organization)
            .WithMany()
            .HasForeignKey(r => r.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Pet)
            .WithMany()
            .HasForeignKey(r => r.PetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Owner)
            .WithMany()
            .HasForeignKey(r => r.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
