using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;

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
            .HasDefaultValue(AppointmentStatus.Reserved);

        builder.Property(a => a.Notes)
            .HasColumnType("text");

        builder.Property(a => a.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(a => a.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.HasIndex(a => a.AppointmentSlotId)
            .IsUnique();

        builder.HasIndex(a => a.OrganizationId);
        builder.HasIndex(a => a.PetId);
        builder.HasIndex(a => new { a.VeterinarianId, a.Date });
        builder.HasIndex(a => a.VeterinarianId);
        builder.HasIndex(a => a.Date);
        builder.HasIndex(a => a.Status);

        builder.HasOne(a => a.Organization)
            .WithMany()
            .HasForeignKey(a => a.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Veterinarian)
            .WithMany(v => v.Appointments)
            .HasForeignKey(a => a.VeterinarianId)
            .OnDelete(DeleteBehavior.Restrict);

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
