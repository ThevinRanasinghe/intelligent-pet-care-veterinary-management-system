using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Configurations;

public class InventoryTransactionConfiguration : IEntityTypeConfiguration<InventoryTransaction>
{
    public void Configure(EntityTypeBuilder<InventoryTransaction> builder)
    {
        // Deliberately no check constraint pinning QuantityChange's sign to
        // Type: StockIn/Return are positive, Dispense/Damaged/Expired/
        // Reservation are negative, ReservationRelease is positive again.
        // That mapping lives in InventoryService, where the type is chosen;
        // the table just stores the signed result.
        builder.ToTable("InventoryTransactions");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(t => t.Type).IsRequired().HasMaxLength(30).HasConversion<string>();
        builder.Property(t => t.Notes).HasMaxLength(500);
        builder.Property(t => t.OccurredAt).IsRequired();

        builder.HasIndex(t => t.MedicineId);
        builder.HasIndex(t => t.OccurredAt);

        // This table is the audit trail: rows are never updated or deleted,
        // and its FKs are Restrict, not Cascade, so a medicine/batch/
        // reservation can never take its own history down with it.
        builder.HasOne(t => t.Medicine)
            .WithMany()
            .HasForeignKey(t => t.MedicineId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Batch)
            .WithMany()
            .HasForeignKey(t => t.BatchId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Reservation)
            .WithMany()
            .HasForeignKey(t => t.ReservationId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}