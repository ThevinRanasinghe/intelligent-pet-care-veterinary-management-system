using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Configurations;

public class MedicineBatchConfiguration : IEntityTypeConfiguration<MedicineBatch>
{
    public void Configure(EntityTypeBuilder<MedicineBatch> builder)
    {
        builder.ToTable("MedicineBatches", t =>
            t.HasCheckConstraint("CK_MedicineBatch_Quantity_NonNegative", "\"Quantity\" >= 0"));

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(b => b.BatchNumber).IsRequired().HasMaxLength(50);
        builder.Property(b => b.Status).IsRequired().HasMaxLength(20).HasConversion<string>();

        builder.Ignore(b => b.IsExpired);
        builder.Ignore(b => b.IsUsable);

        builder.HasIndex(b => new { b.MedicineId, b.ExpiryDate }); // supports FEFO ordering

        builder.HasOne(b => b.Medicine)
            .WithMany(m => m.Batches)
            .HasForeignKey(b => b.MedicineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Supplier)
            .WithMany(s => s.Batches)
            .HasForeignKey(b => b.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}