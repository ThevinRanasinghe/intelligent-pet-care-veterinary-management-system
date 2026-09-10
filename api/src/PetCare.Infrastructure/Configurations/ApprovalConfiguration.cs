using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;

namespace PetCare.Infrastructure.Configurations;

public class ApprovalConfiguration : IEntityTypeConfiguration<Approval>
{
    public void Configure(EntityTypeBuilder<Approval> builder)
    {
        builder.ToTable("Approvals", t =>
        {
            t.HasCheckConstraint(
                "CK_Approval_ReviewedBy_Required_When_Decided",
                "\"Status\" = 'Pending' OR \"ReviewedBy\" IS NOT NULL");

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
            .HasDefaultValue(ApprovalStatus.Pending);

        builder.Property(a => a.ReviewedBy);

        builder.Property(a => a.ReviewedAt);

        builder.Property(a => a.Comment)
            .HasColumnType("text");

        builder.HasIndex(a => a.QuotationId)
            .IsUnique();

        builder.HasIndex(a => a.Status);

        builder.HasOne(a => a.Quotation)
            .WithOne(q => q.Approval)
            .HasForeignKey<Approval>(a => a.QuotationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(a => a.Histories)
            .WithOne(h => h.Approval)
            .HasForeignKey(h => h.ApprovalId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
