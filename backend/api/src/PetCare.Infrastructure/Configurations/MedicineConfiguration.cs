using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Configurations;

public class MedicineConfiguration : IEntityTypeConfiguration<Medicine>
{
    public void Configure(EntityTypeBuilder<Medicine> builder)
    {
        builder.ToTable("Medicines", t =>
        {
            t.HasCheckConstraint("CK_Medicine_TotalQuantity_NonNegative", "\"TotalQuantity\" >= 0");
            t.HasCheckConstraint("CK_Medicine_ReservedQuantity_NonNegative", "\"ReservedQuantity\" >= 0");
            t.HasCheckConstraint("CK_Medicine_Reserved_NotExceed_Total", "\"ReservedQuantity\" <= \"TotalQuantity\"");
            t.HasCheckConstraint("CK_Medicine_UnitPrice_NonNegative", "\"UnitPrice\" >= 0");
            t.HasCheckConstraint("CK_Medicine_ReorderLevel_NonNegative", "\"ReorderLevel\" >= 0");
        });

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(m => m.Name).IsRequired().HasMaxLength(200);
        builder.Property(m => m.Category).IsRequired().HasMaxLength(100);
        builder.Property(m => m.Manufacturer).HasMaxLength(200);
        builder.Property(m => m.UnitPrice).IsRequired().HasColumnType("numeric(10,2)");
        builder.Property(m => m.Status).IsRequired().HasMaxLength(20).HasConversion<string>();

        builder.Ignore(m => m.AvailableQuantity);
        builder.Ignore(m => m.IsLowStock);

        builder.HasIndex(m => m.Name);
        builder.HasIndex(m => m.Status);
    }
}