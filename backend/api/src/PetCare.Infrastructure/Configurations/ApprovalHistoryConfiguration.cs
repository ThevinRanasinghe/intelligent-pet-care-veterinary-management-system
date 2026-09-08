using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Configurations;

public class ApprovalHistoryConfiguration : IEntityTypeConfiguration<ApprovalHistory>
{
    public void Configure(EntityTypeBuilder<ApprovalHistory> builder)
    {
        builder.ToTable("ApprovalHistories");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(h => h.PreviousStatus)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(h => h.NewStatus)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(h => h.ChangedBy)
            .IsRequired();

        builder.Property(h => h.Reason)
            .HasColumnType("text");

        builder.Property(h => h.ChangedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.HasIndex(h => new { h.ApprovalId, h.ChangedAt })
            .HasDatabaseName("IX_ApprovalHistory_ApprovalId_ChangedAt");

        builder.HasOne(h => h.Approval)
            .WithMany(a => a.History)
            .HasForeignKey(h => h.ApprovalId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
