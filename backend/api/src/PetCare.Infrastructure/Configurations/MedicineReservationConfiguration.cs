using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Configurations;

public class MedicineReservationConfiguration : IEntityTypeConfiguration<MedicineReservation>
{
    public void Configure(EntityTypeBuilder<MedicineReservation> builder)
    {
        builder.ToTable("MedicineReservations", t =>
            t.HasCheckConstraint("CK_MedicineReservation_Quantity_Positive", "\"Quantity\" > 0"));

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(r => r.Status).IsRequired().HasMaxLength(20).HasConversion<string>();
        builder.Property(r => r.ReferenceType).HasMaxLength(50);

        builder.HasIndex(r => r.MedicineId);
        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => new { r.ReferenceType, r.ReferenceId }); // lets other modules trace back

        builder.HasOne(r => r.Medicine)
            .WithMany(m => m.Reservations)
            .HasForeignKey(r => r.MedicineId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}