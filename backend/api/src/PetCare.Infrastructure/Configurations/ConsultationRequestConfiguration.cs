using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Configurations;

public class ConsultationRequestConfiguration : IEntityTypeConfiguration<ConsultationRequest>
{
    public void Configure(EntityTypeBuilder<ConsultationRequest> builder)
    {
        builder.ToTable("ConsultationRequests", t =>
        {
            t.HasCheckConstraint(
                "CK_ConsultationRequest_RequestType_Allowed",
                "\"RequestType\" IN ('Initial', 'FollowUp')");
        });

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

        builder.Property(x => x.RequestType)
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue("Initial");

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

        // ConsultationRequest -> Veterinarian (follow-up requester, optional)
        builder.HasOne(x => x.RequestedByVeterinarian)
            .WithMany()
            .HasForeignKey(x => x.RequestedByVeterinarianId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.RequestedByVeterinarianId)
            .HasDatabaseName("IX_ConsultationRequests_RequestedByVeterinarianId");

        // ConsultationRequest -> Organization (booked clinic, optional for
        // legacy rows; SetNull so removing an org keeps the request).
        builder.HasOne(x => x.Organization)
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.OrganizationId)
            .HasDatabaseName("IX_ConsultationRequests_OrganizationId");
    }
}
