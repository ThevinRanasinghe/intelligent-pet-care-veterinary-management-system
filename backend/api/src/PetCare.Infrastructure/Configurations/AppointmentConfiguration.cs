using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Configurations;

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("Appointments", t =>
        {
            t.HasCheckConstraint(
                "CK_Appointment_EndTime_After_StartTime",
                "\"EndTime\" > \"StartTime\"");
        });

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        // PetId must exist: FK to the Pet entity owned by another module.
        // Enforced as a plain NOT NULL Guid column here; the cross-module FK
        // constraint is added at the database level once the Pet table exists
        // (see docs/database/scheduling-billing-approval-domain-model.md).
        builder.Property(a => a.PetId)
            .IsRequired();

        builder.Property(a => a.Date)
            .IsRequired()
            .HasColumnType("date");

        builder.Property(a => a.StartTime)
            .IsRequired()
            .HasColumnType("time");

        builder.Property(a => a.EndTime)
            .IsRequired()
            .HasColumnType("time");

        builder.Property(a => a.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>()
            .HasDefaultValue(Domain.Enums.AppointmentStatus.Reserved);

        builder.Property(a => a.Notes)
            .HasColumnType("text");

        builder.Property(a => a.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(a => a.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        // AppointmentSlotId is UNIQUE: one confirmed appointment consumes exactly one slot.
        builder.HasIndex(a => a.AppointmentSlotId)
            .IsUnique();

        builder.HasIndex(a => a.PetId)
            .HasDatabaseName("IX_Appointment_PetId");

        builder.HasIndex(a => new { a.VeterinarianId, a.Date })
            .HasDatabaseName("IX_Appointment_VeterinarianId_Date");

        builder.HasIndex(a => a.VeterinarianId)
            .HasDatabaseName("IX_Appointment_VeterinarianId");

        builder.HasIndex(a => a.Date)
            .HasDatabaseName("IX_Appointment_ScheduledStart");

        builder.HasIndex(a => a.Status)
            .HasDatabaseName("IX_Appointment_Status");

        // VeterinarianId must exist.
        builder.HasOne(a => a.Veterinarian)
            .WithMany(v => v.Appointments)
            .HasForeignKey(a => a.VeterinarianId)
            .OnDelete(DeleteBehavior.Restrict);

        // AppointmentSlotId must exist.
        builder.HasOne(a => a.AppointmentSlot)
            .WithOne(s => s.Appointment)
            .HasForeignKey<Appointment>(a => a.AppointmentSlotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Quotation)
            .WithOne(q => q.Appointment)
            .HasForeignKey<Quotation>(q => q.AppointmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
