using Microsoft.EntityFrameworkCore;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;

namespace PetCare.Infrastructure.Seed;

/// <summary>
/// Development/test-only fixture data: sample Appointments, a Quotation with
/// line items, and one pending Approval. Per
/// docs/database/scheduling-billing-approval-domain-model.md#seed-data these
/// transactional rows must NOT be part of production seed data (they are not
/// registered via EF Core migration HasData); call SeedAsync explicitly from
/// a Development-only startup path (e.g. only when
/// IHostEnvironment.IsDevelopment() is true).
/// </summary>
public static class DevelopmentSeeder
{
    /// <summary>Default dev Clinic Manager login. Change via user-secrets/env in real environments.</summary>
    public const string DevClinicManagerEmail = "manager@petcare.lk";
    public const string DevClinicManagerPassword = "ChangeMe123!";

    public static async Task SeedAsync(PetCareDbContext context, IPasswordHasher passwordHasher, CancellationToken cancellationToken = default)
    {
        await SeedUsersAsync(context, passwordHasher, cancellationToken);

        if (await context.Appointments.AnyAsync(a => a.Id == SeedIds.DevAppointmentConfirmed, cancellationToken))
        {
            // Already seeded.
            return;
        }

        var seedDate = new DateOnly(2025, 6, 2);

        var confirmedSlot = await context.AppointmentSlots.SingleAsync(s => s.Id == SeedIds.SlotAnikaPerera1, cancellationToken);
        confirmedSlot.Status = AppointmentSlotStatus.Confirmed;

        var reservedSlot = await context.AppointmentSlots.SingleAsync(s => s.Id == SeedIds.SlotRohanFernando1, cancellationToken);
        reservedSlot.Status = AppointmentSlotStatus.Reserved;

        var confirmedAppointment = new Appointment
        {
            Id = SeedIds.DevAppointmentConfirmed,
            PetId = SeedIds.DevPetBuddy,
            VeterinarianId = SeedIds.VetAnikaPerera,
            AppointmentSlotId = SeedIds.SlotAnikaPerera1,
            Date = seedDate,
            StartTime = confirmedSlot.StartTime,
            EndTime = confirmedSlot.EndTime,
            Status = AppointmentStatus.Confirmed,
            Notes = "Dev/test fixture: annual checkup."
        };

        var reservedAppointment = new Appointment
        {
            Id = SeedIds.DevAppointmentReserved,
            PetId = SeedIds.DevPetMisty,
            VeterinarianId = SeedIds.VetRohanFernando,
            AppointmentSlotId = SeedIds.SlotRohanFernando1,
            Date = seedDate,
            StartTime = reservedSlot.StartTime,
            EndTime = reservedSlot.EndTime,
            Status = AppointmentStatus.Reserved,
            Notes = "Dev/test fixture: pending consultation."
        };

        var quotationItems = new List<QuotationItem>
        {
            new()
            {
                Id = SeedIds.DevQuotationItemConsultation,
                QuotationId = SeedIds.DevQuotation,
                Category = "Consultation",
                Description = "General consultation",
                Quantity = 1,
                UnitPrice = 20.00m,
                TotalPrice = 20.00m
            },
            new()
            {
                Id = SeedIds.DevQuotationItemTreatment,
                QuotationId = SeedIds.DevQuotation,
                Category = "Treatment",
                Description = "Wound dressing",
                Quantity = 1,
                UnitPrice = 50.00m,
                TotalPrice = 50.00m
            }
        };

        var quotation = new Quotation
        {
            Id = SeedIds.DevQuotation,
            AppointmentId = SeedIds.DevAppointmentConfirmed,
            Budget = 100.00m,
            Subtotal = 70.00m,
            Total = 70.00m,
            Status = QuotationStatus.PendingApproval
        };

        var pendingApproval = new Approval
        {
            Id = SeedIds.DevApprovalPending,
            QuotationId = SeedIds.DevQuotation,
            Status = ApprovalStatus.Pending,
            ReviewedBy = null,
            ReviewedAt = null,
            Comment = null
        };

        await context.Appointments.AddRangeAsync(new[] { confirmedAppointment, reservedAppointment }, cancellationToken);
        await context.Quotations.AddAsync(quotation, cancellationToken);
        await context.QuotationItems.AddRangeAsync(quotationItems, cancellationToken);
        await context.Approvals.AddAsync(pendingApproval, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Seeds one dev Clinic Manager account so /api/auth/login is
    /// exercisable locally without a manual signup step. Uses the real
    /// IPasswordHasher (not a fixed hash) since Rfc2898-based hashes are
    /// salted per call and cannot be reproduced via static HasData.
    /// </summary>
    private static async Task SeedUsersAsync(PetCareDbContext context, IPasswordHasher passwordHasher, CancellationToken cancellationToken)
    {
        if (await context.Users.AnyAsync(u => u.Id == SeedIds.DevClinicManagerUser, cancellationToken))
        {
            return;
        }

        await context.Users.AddAsync(new User
        {
            Id = SeedIds.DevClinicManagerUser,
            Email = DevClinicManagerEmail,
            PasswordHash = passwordHasher.HashPassword(DevClinicManagerPassword),
            Name = "Miran Perera",
            Role = Roles.ClinicManager,
            Active = true
        }, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
    }
}
