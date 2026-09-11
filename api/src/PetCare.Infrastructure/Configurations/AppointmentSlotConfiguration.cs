using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;

namespace PetCare.Infrastructure.Configurations;

public class AppointmentSlotConfiguration : IEntityTypeConfiguration<AppointmentSlot>
{
    public void Configure(EntityTypeBuilder<AppointmentSlot> builder)
    {
        builder.ToTable("AppointmentSlots", t =>
        {
            t.HasCheckConstraint(
                "CK_AppointmentSlot_EndTime_After_StartTime",
                "\"EndTime\" > \"StartTime\"");
        });

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(s => s.Date)
            .IsRequired()
            .HasColumnType("date");

        builder.Property(s => s.StartTime)
            .IsRequired()
            .HasColumnType("time");

        builder.Property(s => s.EndTime)
            .IsRequired()
            .HasColumnType("time");

        builder.Property(s => s.Branch)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion<string>()
            .HasDefaultValue(AppointmentSlotStatus.Available);

        builder.Property(s => s.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(s => s.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.HasIndex(s => new { s.VeterinarianId, s.Date, s.StartTime })
            .IsUnique();

        builder.HasIndex(s => new { s.VeterinarianId, s.Date });
        builder.HasIndex(s => s.StartTime);

        builder.HasOne(s => s.Veterinarian)
            .WithMany(v => v.AppointmentSlots)
            .HasForeignKey(s => s.VeterinarianId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
