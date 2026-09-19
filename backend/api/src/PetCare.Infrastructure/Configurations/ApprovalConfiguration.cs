using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Configurations;

public class ApprovalConfiguration : IEntityTypeConfiguration<Approval>
{
    public void Configure(EntityTypeBuilder<Approval> builder)
    {
        builder.ToTable("Approvals", t =>
        {
            // ReviewedBy is required once a decision (Approved/Rejected/RevisionRequested) has been made.
            t.HasCheckConstraint(
                "CK_Approval_ReviewedBy_Required_When_Decided",
                "\"Status\" = 'Pending' OR \"ReviewedBy\" IS NOT NULL");

            // Reason (Comment) is required for Rejected/RevisionRequested decisions.
            t.HasCheckConstraint(
                "CK_Approval_Comment_Required_For_Reject_Or_Revision",
                "\"Status\" NOT IN ('Rejected', 'RevisionRequested') OR (\"Comment\" IS NOT NULL AND length(btrim(\"Comment\")) > 0)");
        });

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(a => a.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>()
            .HasDefaultValue(Domain.Enums.ApprovalStatus.Pending);

        builder.Property(a => a.ReviewedBy);

        builder.Property(a => a.ReviewedAt);

        builder.Property(a => a.Comment)
            .HasColumnType("text");

        // 1:1 with Quotation; UNIQUE FK.
        builder.HasIndex(a => a.QuotationId)
            .IsUnique();

        builder.HasIndex(a => a.Status)
            .HasDatabaseName("IX_Approval_Status");

        builder.HasOne(a => a.Quotation)
            .WithOne(q => q.Approval)
            .HasForeignKey<Approval>(a => a.QuotationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(a => a.History)
            .WithOne(h => h.Approval)
            .HasForeignKey(h => h.ApprovalId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
