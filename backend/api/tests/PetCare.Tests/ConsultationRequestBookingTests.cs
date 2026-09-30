using Microsoft.EntityFrameworkCore;
using PetCare.Application.DTOs.Consultations;
using PetCare.Application.Exceptions;
using PetCare.Domain.Constants;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;
using PetCare.Infrastructure;
using PetCare.Infrastructure.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace PetCare.Tests;

/// <summary>
/// InMemory-backed tests for the booking rules in ConsultationRequestService:
/// mandatory organization/date/slot, org-level slot capacity, and
/// organization-scoped reads for staff callers.
/// </summary>
public class ConsultationRequestBookingTests
{
    private static PetCareDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<PetCareDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PetCareDbContext(options);
    }

    private static readonly Guid OrgA = Guid.NewGuid();
    private static readonly Guid OrgB = Guid.NewGuid();
    private static readonly Guid VetA1 = Guid.NewGuid();
    private static readonly Guid VetA2 = Guid.NewGuid();

    // Always a working-hour slot a few days out so "past" rules never fire.
    private static readonly DateTime FutureDate = DateTime.Today.AddDays(5);
    private static readonly TimeSpan TenAM = new(10, 0, 0);

    private static TestTenantContext ScopedManager(Guid orgId) => new()
    {
        UserId = Guid.NewGuid(),
        Role = Roles.ClinicManager,
        IsPlatformAdmin = false,
        IsOrganizationScoped = true,
        OrganizationId = orgId
    };

    private static async Task SeedOwnerPetAndOrgs(PetCareDbContext context)
    {
        context.PetOwners.Add(new PetOwner
        {
            Id = "OWN-1",
            FullName = "Amal Perera",
            Email = "amal@x.lk"
        });
        context.Pets.Add(new Pet
        {
            Id = "PET-1",
            OwnerId = "OWN-1",
            Name = "Shadow",
            Species = "Dog",
            Breed = "Mixed"
        });
        context.Organizations.AddRange(
            new Organization
            {
                Id = OrgA,
                Name = "Happy Paws",
                Email = "a@clinic.lk",
                Phone = "011",
                Address = "1 Main St",
                City = "Colombo",
                Country = "LK",
                Status = OrganizationStatus.Active,
                IsActive = true
            },
            new Organization
            {
                Id = OrgB,
                Name = "Other Clinic",
                Email = "b@clinic.lk",
                Phone = "011",
                Address = "2 Side St",
                City = "Kandy",
                Country = "LK",
                Status = OrganizationStatus.Active,
                IsActive = true
            });
        context.Veterinarians.AddRange(
            new Veterinarian { Id = VetA1, Name = "Dr. A", Specialisation = "General", Branch = "Main", Active = true, OrganizationId = OrgA },
            new Veterinarian { Id = VetA2, Name = "Dr. B", Specialisation = "General", Branch = "Main", Active = true, OrganizationId = OrgA });
        await context.SaveChangesAsync();
    }

    private static CreateConsultationRequestDto ValidCreate() => new()
    {
        PetId = "PET-1",
        OwnerId = "OWN-1",
        Symptoms = "Limping",
        OrganizationId = OrgA,
        PreferredDate = FutureDate,
        PreferredTime = TenAM
    };

    private static Appointment BusyAppointment(Guid vetId, DateOnly date, int startHour) => new()
    {
        Id = Guid.NewGuid(),
        PetId = "PET-1",
        VeterinarianId = vetId,
        AppointmentSlotId = Guid.NewGuid(),
        Date = date,
        StartTime = new TimeOnly(startHour, 0),
        EndTime = new TimeOnly(startHour + 1, 0),
        Status = AppointmentStatus.Confirmed
    };

    // ---------------------------------------------------------------
    // Create — mandatory fields
    // ---------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_MissingOrganization_ThrowsArgument()
    {
        using var context = GetInMemoryDbContext();
        await SeedOwnerPetAndOrgs(context);
        var service = new ConsultationRequestService(context, TestTenantContext.Unscoped);

        var dto = ValidCreate();
        dto.OrganizationId = Guid.Empty;

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(dto));
    }

    [Fact]
    public async Task CreateAsync_MissingPreferredDate_ThrowsArgument()
    {
        using var context = GetInMemoryDbContext();
        await SeedOwnerPetAndOrgs(context);
        var service = new ConsultationRequestService(context, TestTenantContext.Unscoped);

        var dto = ValidCreate();
        dto.PreferredDate = null;

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(dto));
    }

    [Fact]
    public async Task CreateAsync_MissingPreferredTime_ThrowsArgument()
    {
        using var context = GetInMemoryDbContext();
        await SeedOwnerPetAndOrgs(context);
        var service = new ConsultationRequestService(context, TestTenantContext.Unscoped);

        var dto = ValidCreate();
        dto.PreferredTime = null;

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(dto));
    }

    [Fact]
    public async Task CreateAsync_PastDate_ThrowsArgument()
    {
        using var context = GetInMemoryDbContext();
        await SeedOwnerPetAndOrgs(context);
        var service = new ConsultationRequestService(context, TestTenantContext.Unscoped);

        var dto = ValidCreate();
        dto.PreferredDate = DateTime.Today.AddDays(-1);

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(dto));
    }

    [Fact]
    public async Task CreateAsync_NonHourAlignedTime_ThrowsArgument()
    {
        using var context = GetInMemoryDbContext();
        await SeedOwnerPetAndOrgs(context);
        var service = new ConsultationRequestService(context, TestTenantContext.Unscoped);

        var dto = ValidCreate();
        dto.PreferredTime = new TimeSpan(9, 30, 0);

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(dto));
    }

    [Fact]
    public async Task CreateAsync_UnknownOrganization_ThrowsNotFound()
    {
        using var context = GetInMemoryDbContext();
        await SeedOwnerPetAndOrgs(context);
        var service = new ConsultationRequestService(context, TestTenantContext.Unscoped);

        var dto = ValidCreate();
        dto.OrganizationId = Guid.NewGuid();

        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateAsync(dto));
    }

    [Fact]
    public async Task CreateAsync_InactiveOrganization_ThrowsArgument()
    {
        using var context = GetInMemoryDbContext();
        await SeedOwnerPetAndOrgs(context);
        var inactiveOrg = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Pending Clinic",
            Email = "c@clinic.lk",
            Phone = "011",
            Address = "3 Lane",
            City = "Galle",
            Country = "LK",
            Status = OrganizationStatus.Pending,
            IsActive = false
        };
        context.Organizations.Add(inactiveOrg);
        await context.SaveChangesAsync();
        var service = new ConsultationRequestService(context, TestTenantContext.Unscoped);

        var dto = ValidCreate();
        dto.OrganizationId = inactiveOrg.Id;

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(dto));
    }

    // ---------------------------------------------------------------
    // Create — slot capacity
    // ---------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_AllOrgVetsBusy_ThrowsSchedulingConflict()
    {
        using var context = GetInMemoryDbContext();
        await SeedOwnerPetAndOrgs(context);
        var date = DateOnly.FromDateTime(FutureDate);
        context.Appointments.AddRange(
            BusyAppointment(VetA1, date, 10),
            BusyAppointment(VetA2, date, 10));
        await context.SaveChangesAsync();

        var service = new ConsultationRequestService(context, TestTenantContext.Unscoped);

        var ex = await Assert.ThrowsAsync<SchedulingConflictException>(
            () => service.CreateAsync(ValidCreate()));
        Assert.Equal(BookingRules.SlotUnavailableMessage, ex.Message);
    }

    [Fact]
    public async Task CreateAsync_OneVetFree_SucceedsWithOrganizationAndSlot()
    {
        using var context = GetInMemoryDbContext();
        await SeedOwnerPetAndOrgs(context);
        var date = DateOnly.FromDateTime(FutureDate);
        // VetA1 busy at 10-11, VetA2 free → the slot is still bookable.
        context.Appointments.Add(BusyAppointment(VetA1, date, 10));
        await context.SaveChangesAsync();

        var service = new ConsultationRequestService(context, TestTenantContext.Unscoped);

        var result = await service.CreateAsync(ValidCreate());

        Assert.Equal(OrgA, result.OrganizationId);
        Assert.Equal("Happy Paws", result.OrganizationName);
        Assert.Equal(TenAM, result.PreferredTime);
        Assert.Equal("Draft", result.Status);

        var saved = await context.ConsultationRequests.FindAsync(result.Id);
        Assert.Equal(OrgA, saved!.OrganizationId);
        // The booked slot start is stored inside PreferredDate.
        Assert.Equal(new TimeOnly(10, 0), TimeOnly.FromDateTime(saved.PreferredDate));
    }

    // ---------------------------------------------------------------
    // Submit — slot filled since create
    // ---------------------------------------------------------------

    [Fact]
    public async Task SubmitAsync_SlotBecameFull_ThrowsSchedulingConflict()
    {
        using var context = GetInMemoryDbContext();
        await SeedOwnerPetAndOrgs(context);
        var service = new ConsultationRequestService(context, TestTenantContext.Unscoped);

        var created = await service.CreateAsync(ValidCreate());

        // Both vets get booked at 10:00 after the request was created.
        var date = DateOnly.FromDateTime(FutureDate);
        context.Appointments.AddRange(
            BusyAppointment(VetA1, date, 10),
            BusyAppointment(VetA2, date, 10));
        await context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<SchedulingConflictException>(
            () => service.SubmitAsync(created.Id));
        Assert.Equal(BookingRules.SlotUnavailableMessage, ex.Message);
    }

    // ---------------------------------------------------------------
    // Organization scoping
    // ---------------------------------------------------------------

    private static async Task SeedConsultations(PetCareDbContext context)
    {
        context.ConsultationRequests.AddRange(
            new ConsultationRequest
            {
                Id = "CON-A1",
                PetId = "PET-1",
                OwnerId = "OWN-1",
                OrganizationId = OrgA,
                SymptomsDescription = "Limping",
                PreferredDate = FutureDate,
                Status = "Submitted"
            },
            new ConsultationRequest
            {
                Id = "CON-B1",
                PetId = "PET-1",
                OwnerId = "OWN-1",
                OrganizationId = OrgB,
                SymptomsDescription = "Coughing",
                PreferredDate = FutureDate,
                Status = "Submitted"
            });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetAllAsync_OrganizationScopedStaff_SeesOnlyOwnOrg()
    {
        using var context = GetInMemoryDbContext();
        await SeedOwnerPetAndOrgs(context);
        await SeedConsultations(context);

        var service = new ConsultationRequestService(context, ScopedManager(OrgA));
        var results = await service.GetAllAsync();

        Assert.Single(results);
        Assert.Equal("CON-A1", results[0].Id);
        Assert.Equal(OrgA, results[0].OrganizationId);
    }

    [Fact]
    public async Task GetByIdAsync_ForeignOrganization_ReturnsNull()
    {
        using var context = GetInMemoryDbContext();
        await SeedOwnerPetAndOrgs(context);
        await SeedConsultations(context);

        var service = new ConsultationRequestService(context, ScopedManager(OrgA));

        Assert.Null(await service.GetByIdAsync("CON-B1"));
        Assert.NotNull(await service.GetByIdAsync("CON-A1"));
    }

    [Fact]
    public async Task GetAllAsync_UnscopedCaller_SeesAllOrganizations()
    {
        using var context = GetInMemoryDbContext();
        await SeedOwnerPetAndOrgs(context);
        await SeedConsultations(context);

        var service = new ConsultationRequestService(context, TestTenantContext.Unscoped);
        var results = await service.GetAllAsync();

        Assert.Equal(2, results.Count);
    }
}
