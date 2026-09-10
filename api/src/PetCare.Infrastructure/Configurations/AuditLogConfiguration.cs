using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(l => l.OrganizationId)
            .IsRequired();

        builder.Property(l => l.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(l => l.UserEmail)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(l => l.Action)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(l => l.EntityType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(l => l.EntityId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(l => l.Details)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(l => l.Timestamp)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.HasIndex(l => l.OrganizationId);
        builder.HasIndex(l => l.Action);
        builder.HasIndex(l => l.Timestamp);

        builder.HasOne(l => l.Organization)
            .WithMany()
            .HasForeignKey(l => l.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
