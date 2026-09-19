using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetCare.Domain.Entities;
using PetCare.Infrastructure.Seed;

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
            .HasDefaultValue(Domain.Enums.AppointmentSlotStatus.Available);

        builder.Property(s => s.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(s => s.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Prevents double-booking the same vet at the same start time.
        builder.HasIndex(s => new { s.VeterinarianId, s.Date, s.StartTime })
            .IsUnique();

        // Fast conflict/availability lookups.
        builder.HasIndex(s => new { s.VeterinarianId, s.Date })
            .HasDatabaseName("IX_AppointmentSlot_VeterinarianId_Date");

        builder.HasIndex(s => s.StartTime)
            .HasDatabaseName("IX_AppointmentSlot_StartTime");

        builder.HasOne(s => s.Veterinarian)
            .WithMany(v => v.AppointmentSlots)
            .HasForeignKey(s => s.VeterinarianId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Appointment)
            .WithOne(a => a.AppointmentSlot)
            .HasForeignKey<Appointment>(a => a.AppointmentSlotId)
            .OnDelete(DeleteBehavior.Restrict);

        // Development/reference seed data: one or two Available slots per seeded vet.
        // See docs/database/scheduling-billing-approval-domain-model.md#seed-data.
        var seedCreatedAt = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var seedDate = new DateOnly(2025, 6, 2);

        builder.HasData(
            new
            {
                Id = SeedIds.SlotAnikaPerera1,
                VeterinarianId = SeedIds.VetAnikaPerera,
                Date = seedDate,
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(9, 30),
                Branch = "Colombo",
                Status = Domain.Enums.AppointmentSlotStatus.Available,
                CreatedAt = seedCreatedAt,
                UpdatedAt = seedCreatedAt
            },
            new
            {
                Id = SeedIds.SlotAnikaPerera2,
                VeterinarianId = SeedIds.VetAnikaPerera,
                Date = seedDate,
                StartTime = new TimeOnly(10, 0),
                EndTime = new TimeOnly(10, 30),
                Branch = "Colombo",
                Status = Domain.Enums.AppointmentSlotStatus.Available,
                CreatedAt = seedCreatedAt,
                UpdatedAt = seedCreatedAt
            },
            new
            {
                Id = SeedIds.SlotRohanFernando1,
                VeterinarianId = SeedIds.VetRohanFernando,
                Date = seedDate,
                StartTime = new TimeOnly(11, 0),
                EndTime = new TimeOnly(11, 30),
                Branch = "Kandy",
                Status = Domain.Enums.AppointmentSlotStatus.Available,
                CreatedAt = seedCreatedAt,
                UpdatedAt = seedCreatedAt
            },
            new
            {
                Id = SeedIds.SlotNadeeSilva1,
                VeterinarianId = SeedIds.VetNadeeSilva,
                Date = seedDate,
                StartTime = new TimeOnly(14, 0),
                EndTime = new TimeOnly(14, 30),
                Branch = "Galle",
                Status = Domain.Enums.AppointmentSlotStatus.Available,
                CreatedAt = seedCreatedAt,
                UpdatedAt = seedCreatedAt
            }
        );
    }
}
