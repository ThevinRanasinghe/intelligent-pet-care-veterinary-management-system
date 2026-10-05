using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Configurations;

public class QuotationItemConfiguration : IEntityTypeConfiguration<QuotationItem>
{
    public void Configure(EntityTypeBuilder<QuotationItem> builder)
    {
        builder.ToTable("QuotationItems", t =>
        {
            t.HasCheckConstraint("CK_QuotationItem_Quantity_Positive", "\"Quantity\" > 0");
            t.HasCheckConstraint("CK_QuotationItem_UnitPrice_NonNegative", "\"UnitPrice\" >= 0");
            t.HasCheckConstraint("CK_QuotationItem_TotalPrice_NonNegative", "\"TotalPrice\" >= 0");
            t.HasCheckConstraint(
                "CK_QuotationItem_TotalPrice_Equals_Quantity_Times_UnitPrice",
                "\"TotalPrice\" = \"Quantity\" * \"UnitPrice\"");
            t.HasCheckConstraint(
                "CK_QuotationItem_Category_Allowed",
                "\"Category\" IN ('Consultation', 'Examination', 'Treatment', 'Medicine', 'Other')");
        });

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(i => i.Category)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(i => i.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(i => i.Quantity)
            .IsRequired();

        builder.Property(i => i.UnitPrice)
            .IsRequired()
            .HasColumnType("numeric(10,2)");

        builder.Property(i => i.TotalPrice)
            .IsRequired()
            .HasColumnType("numeric(12,2)");

        builder.HasIndex(i => i.QuotationId)
            .HasDatabaseName("IX_QuotationItem_QuotationId");

        builder.HasOne(i => i.Quotation)
            .WithMany(q => q.Items)
            .HasForeignKey(i => i.QuotationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
