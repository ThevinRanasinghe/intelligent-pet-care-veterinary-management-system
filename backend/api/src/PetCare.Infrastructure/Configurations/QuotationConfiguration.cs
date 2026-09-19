using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Configurations;

public class QuotationConfiguration : IEntityTypeConfiguration<Quotation>
{
    public void Configure(EntityTypeBuilder<Quotation> builder)
    {
        builder.ToTable("Quotations", t =>
        {
            t.HasCheckConstraint("CK_Quotation_Budget_NonNegative", "\"Budget\" >= 0");
            t.HasCheckConstraint("CK_Quotation_Subtotal_NonNegative", "\"Subtotal\" >= 0");
            t.HasCheckConstraint("CK_Quotation_Total_NonNegative", "\"Total\" >= 0");
        });

        builder.HasKey(q => q.Id);

        builder.Property(q => q.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(q => q.Budget)
            .IsRequired()
            .HasColumnType("numeric(12,2)");

        builder.Property(q => q.Subtotal)
            .IsRequired()
            .HasColumnType("numeric(12,2)")
            .HasDefaultValue(0m);

        builder.Property(q => q.Total)
            .IsRequired()
            .HasColumnType("numeric(12,2)")
            .HasDefaultValue(0m);

        builder.Property(q => q.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>()
            .HasDefaultValue(Domain.Enums.QuotationStatus.Draft);

        builder.Property(q => q.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(q => q.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        // 1:1 with Appointment; UNIQUE FK.
        builder.HasIndex(q => q.AppointmentId)
            .IsUnique();

        builder.HasIndex(q => q.Status)
            .HasDatabaseName("IX_Quotation_Status");

        builder.HasMany(q => q.Items)
            .WithOne(i => i.Quotation)
            .HasForeignKey(i => i.QuotationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(q => q.Approval)
            .WithOne(a => a.Quotation)
            .HasForeignKey<Approval>(a => a.QuotationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
