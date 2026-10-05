using Microsoft.EntityFrameworkCore;
using PetCare.Application.DTOs.Consultations;
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
/// InMemory-backed tests for the pet archive/restore lifecycle:
/// archived pets leave the active listing, keep every historical
/// record, are blocked from new workflows, and can be restored.
/// Permanent deletion is only allowed when no protected records exist.
/// </summary>
public class PetArchiveTests
{
    private static PetCareDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<PetCareDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PetCareDbContext(options);
    }

    private static readonly Guid OrgA = Guid.NewGuid();
    private static readonly Guid VetA = Guid.NewGuid();
    private static readonly DateTime FutureDate = DateTime.Today.AddDays(5);

    private static async Task SeedOwnerAndPet(PetCareDbContext context)
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
        await context.SaveChangesAsync();
    }

    /// <summary>Seeds the minimum graph a consultation booking needs.</summary>
    private static async Task SeedForBooking(PetCareDbContext context)
    {
        await SeedOwnerAndPet(context);
        context.Organizations.Add(new Organization
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
        });
        context.Veterinarians.Add(new Veterinarian
        {
            Id = VetA,
            Name = "Dr. A",
            Specialisation = "General",
            Branch = "Main",
            Active = true,
            OrganizationId = OrgA
        });
        await context.SaveChangesAsync();
    }

    // ---------------------------------------------------------------
    // Archive / restore
    // ---------------------------------------------------------------

    [Fact]
    public async Task ArchiveAsync_ActivePet_SetsFlagAndKeepsRecord()
    {
        using var context = GetInMemoryDbContext();
        await SeedOwnerAndPet(context);
        var service = new PetService(context);

        var archived = await service.ArchiveAsync("PET-1");

        Assert.NotNull(archived);
        Assert.True(archived!.IsArchived);
        // The pet still exists — archive never deletes data.
        Assert.True(await context.Pets.AnyAsync(p => p.Id == "PET-1"));
    }

    [Fact]
    public async Task ArchiveAsync_AlreadyArchived_Throws()
    {
        using var context = GetInMemoryDbContext();
        await SeedOwnerAndPet(context);
        var service = new PetService(context);
        await service.ArchiveAsync("PET-1");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ArchiveAsync("PET-1"));
    }

    [Fact]
    public async Task ArchiveAsync_UnknownPet_ReturnsNull()
    {
        using var context = GetInMemoryDbContext();
        var service = new PetService(context);

        Assert.Null(await service.ArchiveAsync("PET-NOPE"));
    }

    [Fact]
    public async Task ArchivedPet_HiddenFromDefaultListings()
    {
        using var context = GetInMemoryDbContext();
        await SeedOwnerAndPet(context);
        var service = new PetService(context);
        await service.ArchiveAsync("PET-1");

        Assert.Empty(await service.GetByOwnerIdAsync("OWN-1"));
        Assert.Empty(await service.GetAllAsync());

        var withArchived = await service.GetByOwnerIdAsync(
            "OWN-1", includeArchived: true);
        Assert.Single(withArchived);
        Assert.True(withArchived[0].IsArchived);
    }

    [Fact]
    public async Task RestoreAsync_ArchivedPet_ReturnsToActiveList()
    {
        using var context = GetInMemoryDbContext();
        await SeedOwnerAndPet(context);
        var service = new PetService(context);
        await service.ArchiveAsync("PET-1");

        var restored = await service.RestoreAsync("PET-1");

        Assert.NotNull(restored);
        Assert.False(restored!.IsArchived);
        Assert.Single(await service.GetByOwnerIdAsync("OWN-1"));
    }

    [Fact]
    public async Task RestoreAsync_NotArchived_Throws()
    {
        using var context = GetInMemoryDbContext();
        await SeedOwnerAndPet(context);
        var service = new PetService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RestoreAsync("PET-1"));
    }

    [Fact]
    public async Task ArchiveAsync_PreservesHistoricalRecords()
    {
        using var context = GetInMemoryDbContext();
        await SeedOwnerAndPet(context);
        context.ConsultationRequests.Add(new ConsultationRequest
        {
            Id = "CON-1",
            PetId = "PET-1",
            OwnerId = "OWN-1",
            SymptomsDescription = "Limping",
            Status = "Submitted"
        });
        context.Examinations.Add(new Examination
        {
            Id = Guid.NewGuid(),
            PetId = "PET-1",
            VeterinarianId = VetA,
            Symptoms = "Checkup"
        });
        await context.SaveChangesAsync();

        var service = new PetService(context);
        await service.ArchiveAsync("PET-1");

        // History is untouched by archiving.
        Assert.True(await context.ConsultationRequests
            .AnyAsync(c => c.Id == "CON-1"));
        Assert.True(await context.Examinations
            .AnyAsync(e => e.PetId == "PET-1"));
    }

    // ---------------------------------------------------------------
    // Permanent delete safety
    // ---------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_PetWithConsultation_BlockedWithBusinessError()
    {
        using var context = GetInMemoryDbContext();
        await SeedOwnerAndPet(context);
        context.ConsultationRequests.Add(new ConsultationRequest
        {
            Id = "CON-1",
            PetId = "PET-1",
            OwnerId = "OWN-1",
            SymptomsDescription = "Limping",
            Status = "Submitted"
        });
        await context.SaveChangesAsync();
        var service = new PetService(context);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteAsync("PET-1"));

        Assert.Contains("archived instead", ex.Message);
        Assert.True(await context.Pets.AnyAsync(p => p.Id == "PET-1"));
        Assert.True(await context.ConsultationRequests
            .AnyAsync(c => c.Id == "CON-1"));
    }

    [Fact]
    public async Task DeleteAsync_PetWithAppointment_Blocked()
    {
        using var context = GetInMemoryDbContext();
        await SeedOwnerAndPet(context);
        context.Appointments.Add(new Appointment
        {
            Id = Guid.NewGuid(),
            PetId = "PET-1",
            VeterinarianId = VetA,
            AppointmentSlotId = Guid.NewGuid(),
            Date = DateOnly.FromDateTime(FutureDate),
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(11, 0),
            Status = AppointmentStatus.Confirmed
        });
        await context.SaveChangesAsync();
        var service = new PetService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteAsync("PET-1"));
        Assert.True(await context.Pets.AnyAsync(p => p.Id == "PET-1"));
    }

    [Fact]
    public async Task DeleteAsync_PetWithExamination_Blocked()
    {
        using var context = GetInMemoryDbContext();
        await SeedOwnerAndPet(context);
        context.Examinations.Add(new Examination
        {
            Id = Guid.NewGuid(),
            PetId = "PET-1",
            VeterinarianId = VetA,
            Symptoms = "Checkup"
        });
        await context.SaveChangesAsync();
        var service = new PetService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteAsync("PET-1"));
        Assert.True(await context.Pets.AnyAsync(p => p.Id == "PET-1"));
    }

    [Fact]
    public async Task DeleteAsync_PetWithoutHistory_Deleted()
    {
        using var context = GetInMemoryDbContext();
        await SeedOwnerAndPet(context);
        var service = new PetService(context);

        var deleted = await service.DeleteAsync("PET-1");

        Assert.True(deleted);
        Assert.False(await context.Pets.AnyAsync(p => p.Id == "PET-1"));
    }

    // ---------------------------------------------------------------
    // Archived pets cannot start new workflows
    // ---------------------------------------------------------------

    private static CreateConsultationRequestDto ValidBooking() => new()
    {
        PetId = "PET-1",
        OwnerId = "OWN-1",
        Symptoms = "Limping",
        OrganizationId = OrgA,
        PreferredDate = FutureDate,
        PreferredTime = new TimeSpan(10, 0, 0)
    };

    [Fact]
    public async Task CreateConsultation_ArchivedPet_Rejected()
    {
        using var context = GetInMemoryDbContext();
        await SeedForBooking(context);
        var pets = new PetService(context);
        await pets.ArchiveAsync("PET-1");

        var service = new ConsultationRequestService(
            context, TestTenantContext.Unscoped);

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateAsync(ValidBooking()));

        Assert.Contains("archived", ex.Message);
        Assert.False(await context.ConsultationRequests.AnyAsync());
    }

    [Fact]
    public async Task CreateConsultation_RestoredPet_Works()
    {
        using var context = GetInMemoryDbContext();
        await SeedForBooking(context);
        var pets = new PetService(context);
        await pets.ArchiveAsync("PET-1");
        await pets.RestoreAsync("PET-1");

        var service = new ConsultationRequestService(
            context, TestTenantContext.Unscoped);

        var created = await service.CreateAsync(ValidBooking());

        Assert.Equal("PET-1", created.PetId);
        Assert.True(await context.ConsultationRequests.AnyAsync());
    }
}
