using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Configurations;

public class AIProposalConfiguration : IEntityTypeConfiguration<AIProposal>
{
    public void Configure(EntityTypeBuilder<AIProposal> builder)
    {
        builder.ToTable("AIProposals");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(p => p.OrganizationId)
            .IsRequired();

        builder.Property(p => p.Status)
            .IsRequired()
            .HasMaxLength(30)
            .HasDefaultValue("Pending");

        builder.Property(p => p.Urgency)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("Routine");

        builder.Property(p => p.PetName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.OwnerName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(p => p.SymptomsSummary)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(p => p.PreliminaryRecommendation)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(p => p.ProposedTreatment)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(p => p.MedicineAvailabilityStatus)
            .IsRequired()
            .HasMaxLength(30)
            .HasDefaultValue("InStock");

        builder.Property(p => p.ProposedVeterinarianName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(p => p.ProposedDate)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.ProposedTime)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.QuotationTotal)
            .IsRequired()
            .HasColumnType("numeric(12,2)");

        builder.Property(p => p.BudgetLimit)
            .IsRequired()
            .HasColumnType("numeric(12,2)");

        builder.Property(p => p.ValidationChecksJson)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(p => p.ExecutionStepsJson)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(p => p.SubmittedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(p => p.DecisionNote)
            .HasColumnType("text");

        builder.HasIndex(p => p.OrganizationId);
        builder.HasIndex(p => p.Status);

        builder.HasOne(p => p.Organization)
            .WithMany()
            .HasForeignKey(p => p.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.ConsultationRequest)
            .WithMany()
            .HasForeignKey(p => p.ConsultationRequestId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(p => p.Appointment)
            .WithMany()
            .HasForeignKey(p => p.AppointmentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(p => p.Quotation)
            .WithMany()
            .HasForeignKey(p => p.QuotationId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
