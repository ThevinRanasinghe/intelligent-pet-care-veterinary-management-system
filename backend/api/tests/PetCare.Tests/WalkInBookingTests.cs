using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PetCare.Application.DTOs.Consultations;
using PetCare.Application.DTOs.PetOwners;
using PetCare.Application.DTOs.Pets;
using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;
using PetCare.Application.Services;
using PetCare.Application.Validators;
using PetCare.Domain.Constants;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;
using PetCare.Infrastructure;
using PetCare.Infrastructure.Repositories;
using PetCare.Infrastructure.Services;
using System;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace PetCare.Tests;

/// <summary>
/// InMemory-backed tests for WalkInBookingService: the walk-in intake must
/// produce the same records as an online booking — owner/pet resolution,
/// a WalkIn-marked consultation that is submitted and assigned through the
/// authoritative workflow (slot reservation + Confirmed appointment linked
/// via ConsultationRequestId + AppointmentConfirmed transition).
/// Real services and repositories run against EF Core InMemory; only the
/// unit-of-work transaction is a no-op fake (InMemory has no transactions).
/// </summary>
public class WalkInBookingTests
{
    private static PetCareDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<PetCareDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PetCareDbContext(options);
    }

    private static readonly Guid OrgA = Guid.NewGuid();
    private static readonly Guid VetA1 = Guid.NewGuid();
    private static readonly Guid VetA2 = Guid.NewGuid();

    private static readonly DateOnly FutureDate = DateOnly.FromDateTime(DateTime.Today.AddDays(5));
    private static readonly TimeOnly TenAM = new(10, 0);

    private sealed class NoopTransaction : IDbContextTransaction
    {
        public Guid TransactionId => Guid.NewGuid();
        public DbTransaction Instance => null!;
        public void Commit() { }
        public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
        public void Rollback() { }
        public Task RollbackAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task CreateSavepointAsync(string name, CancellationToken ct = default) => Task.CompletedTask;
        public Task RollbackToSavepointAsync(string name, CancellationToken ct = default) => Task.CompletedTask;
        public Task ReleaseSavepointAsync(string name, CancellationToken ct = default) => Task.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>Real persistence, no-op transaction (InMemory limitation).</summary>
    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        private readonly PetCareDbContext _context;
        public FakeUnitOfWork(PetCareDbContext context) => _context = context;
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default)
            => Task.FromResult<IDbContextTransaction>(new NoopTransaction());
    }

    private sealed class NullVeterinarianResolver : ICurrentVeterinarianResolver
    {
        public Task<Veterinarian?> TryResolveAsync(CancellationToken ct = default)
            => Task.FromResult<Veterinarian?>(null);
        public Task<Veterinarian> ResolveRequiredAsync(CancellationToken ct = default)
            => throw new InvalidOperationException("No linked veterinarian in tests.");
    }

    private static TestTenantContext ScopedManager(Guid orgId) => new()
    {
        UserId = Guid.NewGuid(),
        Role = Roles.ClinicManager,
        IsPlatformAdmin = false,
        IsOrganizationScoped = true,
        OrganizationId = orgId
    };

    private static WalkInBookingService NewService(
        PetCareDbContext context, ITenantContext tenant, IUnitOfWork uow)
    {
        var owners = new PetOwnerService(context);
        var ownerRepo = new PetOwnerRepository(context);
        var pets = new PetService(context);
        var consultations = new ConsultationRequestService(context, tenant);
        var consultRepo = new ConsultationRequestRepository(context, tenant);
        var workflow = new ConsultationWorkflowService(
            consultRepo,
            new VeterinarianRepository(context, tenant),
            new AppointmentRepository(context, tenant),
            new AppointmentSlotRepository(context, tenant),
            pets,
            new ExaminationRepository(context, tenant),
            new NullVeterinarianResolver(),
            tenant,
            uow,
            new AssignVeterinarianRequestValidator(),
            new CreateFollowUpRequestValidator());

        return new WalkInBookingService(
            new CreateWalkInBookingRequestValidator(),
            owners,
            ownerRepo,
            pets,
            consultations,
            consultRepo,
            workflow,
            tenant,
            uow);
    }

    private static async Task SeedOrgOwnerPet(PetCareDbContext context)
    {
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
        context.Pets.Add(new Pet
        {
            Id = "PET-OTHER",
            OwnerId = "OWN-OTHER",
            Name = "NotOurs",
            Species = "Cat",
            Breed = "Mixed"
        });
        context.PetOwners.Add(new PetOwner
        {
            Id = "OWN-OTHER",
            FullName = "Other Owner",
            Email = "other@x.lk"
        });
        context.Veterinarians.AddRange(
            new Veterinarian { Id = VetA1, Name = "Dr. A", Specialisation = "General", Branch = "Main", Active = true, OrganizationId = OrgA },
            new Veterinarian { Id = VetA2, Name = "Dr. B", Specialisation = "General", Branch = "Main", Active = true, OrganizationId = OrgA });
        await context.SaveChangesAsync();
    }

    private static CreateWalkInBookingRequest ValidRequest() => new()
    {
        OwnerId = "OWN-1",
        PetId = "PET-1",
        Symptoms = "Limping since morning",
        Urgency = "Medium",
        VeterinarianId = VetA1,
        Date = FutureDate,
        StartTime = TenAM
    };

    // 1. Happy path: existing owner + existing pet → the full workflow
    //    produces the same records an online booking produces.
    [Fact]
    public async Task CreateAsync_ExistingOwnerAndPet_CompletesSharedWorkflow()
    {
        using var context = GetInMemoryDbContext();
        await SeedOrgOwnerPet(context);
        var tenant = ScopedManager(OrgA);
        var service = NewService(context, tenant, new FakeUnitOfWork(context));

        var result = await service.CreateAsync(ValidRequest());

        Assert.Equal("WalkIn", result.Consultation.Source);
        Assert.Equal("AppointmentConfirmed", result.Consultation.Status);
        Assert.Equal("OWN-1", result.Consultation.OwnerId);
        Assert.Equal("PET-1", result.Consultation.PetId);
        Assert.Equal(OrgA, result.Consultation.OrganizationId);

        Assert.Equal("Confirmed", result.Appointment.Status);
        Assert.Equal(result.Consultation.Id, result.Appointment.ConsultationRequestId);
        Assert.Equal(VetA1, result.Appointment.VeterinarianId);
        Assert.Equal("PET-1", result.Appointment.PetId);

        var appointment = await context.Appointments
            .SingleAsync(a => a.Id == result.Appointment.Id);
        Assert.Equal(result.Consultation.Id, appointment.ConsultationRequestId);

        var slot = await context.AppointmentSlots
            .SingleAsync(s => s.Id == appointment.AppointmentSlotId);
        Assert.Equal(AppointmentSlotStatus.Reserved, slot.Status);
        Assert.Equal(VetA1, slot.VeterinarianId);

        Assert.True(
            await context.ConsultationStatusHistories
                .AnyAsync(h =>
                    h.ConsultationRequestId == result.Consultation.Id
                    && h.Status == "AppointmentConfirmed"));
    }

    // 2. New owner + new pet are registered with real ids and linked.
    [Fact]
    public async Task CreateAsync_NewOwnerAndNewPet_RegistersAndLinks()
    {
        using var context = GetInMemoryDbContext();
        await SeedOrgOwnerPet(context);
        var tenant = ScopedManager(OrgA);
        var service = NewService(context, tenant, new FakeUnitOfWork(context));

        var request = new CreateWalkInBookingRequest
        {
            NewOwner = new CreatePetOwnerDto
            {
                FullName = "Walk In",
                Email = "walkin@desk.lk",
                PhoneNumber = "077"
            },
            NewPet = new CreatePetDto { Name = "Rex", Species = "Dog", Breed = "Mixed" },
            Symptoms = "Not eating",
            VeterinarianId = VetA2,
            Date = FutureDate,
            StartTime = TenAM
        };

        var result = await service.CreateAsync(request);

        var owner = await context.PetOwners.SingleAsync(o => o.Email == "walkin@desk.lk");
        var pet = await context.Pets.SingleAsync(p => p.Name == "Rex");
        Assert.Equal(owner.Id, pet.OwnerId);
        Assert.Equal(owner.Id, result.Owner.Id);
        Assert.Equal(pet.Id, result.Pet.Id);
        Assert.Equal(owner.Id, result.Consultation.OwnerId);
        Assert.Equal(pet.Id, result.Consultation.PetId);
        Assert.Equal("AppointmentConfirmed", result.Consultation.Status);
    }

    // 3. Registering an owner whose email already exists reuses the record
    //    instead of creating a duplicate.
    [Fact]
    public async Task CreateAsync_NewOwnerEmailExists_ReusesOwner()
    {
        using var context = GetInMemoryDbContext();
        await SeedOrgOwnerPet(context);
        var tenant = ScopedManager(OrgA);
        var service = NewService(context, tenant, new FakeUnitOfWork(context));

        var request = ValidRequest();
        request.OwnerId = null;
        request.NewOwner = new CreatePetOwnerDto
        {
            FullName = "Amal Duplicate",
            Email = "amal@x.lk"
        };

        var result = await service.CreateAsync(request);

        Assert.Equal("OWN-1", result.Owner.Id);
        Assert.Equal(2, context.PetOwners.Count());
    }

    // 3b. Email dedupe is case-insensitive: "AMAL@x.lk" resolves to the
    //     owner stored as "amal@x.lk" — no duplicate profile is created.
    [Fact]
    public async Task CreateAsync_NewOwnerEmailDifferentCase_ReusesOwner()
    {
        using var context = GetInMemoryDbContext();
        await SeedOrgOwnerPet(context);
        var tenant = ScopedManager(OrgA);
        var service = NewService(context, tenant, new FakeUnitOfWork(context));

        var request = ValidRequest();
        request.OwnerId = null;
        request.NewOwner = new CreatePetOwnerDto
        {
            FullName = "Amal Perera",
            Email = "  AMAL@x.lk  "
        };

        var result = await service.CreateAsync(request);

        Assert.Equal("OWN-1", result.Owner.Id);
        Assert.Equal(2, context.PetOwners.Count());
    }

    // 4. A pet that belongs to a different owner is rejected.
    [Fact]
    public async Task CreateAsync_PetBelongsToOtherOwner_Throws()
    {
        using var context = GetInMemoryDbContext();
        await SeedOrgOwnerPet(context);
        var tenant = ScopedManager(OrgA);
        var service = NewService(context, tenant, new FakeUnitOfWork(context));

        var request = ValidRequest();
        request.PetId = "PET-OTHER";

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(request));
        Assert.False(await context.ConsultationRequests.AnyAsync());
    }

    // 5. A conflicting booking on the same veterinarian is rejected by the
    //    shared assign path (double-booking prevention).
    [Fact]
    public async Task CreateAsync_VeterinarianBusy_ThrowsConflict()
    {
        using var context = GetInMemoryDbContext();
        await SeedOrgOwnerPet(context);
        context.Appointments.Add(new Appointment
        {
            Id = Guid.NewGuid(),
            PetId = "PET-1",
            VeterinarianId = VetA1,
            AppointmentSlotId = Guid.NewGuid(),
            Date = FutureDate,
            StartTime = TenAM,
            EndTime = TenAM.AddHours(1),
            Status = AppointmentStatus.Confirmed
        });
        await context.SaveChangesAsync();

        var tenant = ScopedManager(OrgA);
        var service = NewService(context, tenant, new FakeUnitOfWork(context));

        await Assert.ThrowsAsync<SchedulingConflictException>(
            () => service.CreateAsync(ValidRequest()));
        Assert.False(await context.Appointments
            .AnyAsync(a => a.ConsultationRequestId != null));
    }

    // 6. A pre-posted Available slot is adopted rather than duplicated.
    [Fact]
    public async Task CreateAsync_PostedSlot_IsAdopted()
    {
        using var context = GetInMemoryDbContext();
        await SeedOrgOwnerPet(context);
        var posted = new AppointmentSlot
        {
            Id = Guid.NewGuid(),
            VeterinarianId = VetA1,
            Date = FutureDate,
            StartTime = TenAM,
            EndTime = TenAM.AddHours(1),
            Branch = "Main",
            Status = AppointmentSlotStatus.Available
        };
        context.AppointmentSlots.Add(posted);
        await context.SaveChangesAsync();

        var tenant = ScopedManager(OrgA);
        var service = NewService(context, tenant, new FakeUnitOfWork(context));

        var result = await service.CreateAsync(ValidRequest());

        Assert.Equal(posted.Id, result.Appointment.AppointmentSlotId);
        Assert.Equal(AppointmentSlotStatus.Reserved,
            (await context.AppointmentSlots.FindAsync(posted.Id))!.Status);
        Assert.Equal(1, context.AppointmentSlots.Count());
    }

    // 7. Invalid intake is rejected by the validator before any writes.
    [Theory]
    [InlineData("bothOwners")]
    [InlineData("neitherOwner")]
    [InlineData("bothPets")]
    [InlineData("neitherPet")]
    [InlineData("missingSymptoms")]
    [InlineData("pastSlot")]
    [InlineData("nonAligned")]
    [InlineData("missingVet")]
    public async Task CreateAsync_InvalidRequest_ValidationFails(string scenario)
    {
        using var context = GetInMemoryDbContext();
        await SeedOrgOwnerPet(context);
        var tenant = ScopedManager(OrgA);
        var service = NewService(context, tenant, new FakeUnitOfWork(context));

        var request = ValidRequest();
        switch (scenario)
        {
            case "bothOwners":
                request.NewOwner = new CreatePetOwnerDto { FullName = "X", Email = "x@x.lk" };
                break;
            case "neitherOwner":
                request.OwnerId = null;
                break;
            case "bothPets":
                request.NewPet = new CreatePetDto { Name = "X", Species = "Dog", Breed = "Mixed" };
                break;
            case "neitherPet":
                request.PetId = null;
                break;
            case "missingSymptoms":
                request.Symptoms = "";
                break;
            case "pastSlot":
                request.Date = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
                break;
            case "nonAligned":
                request.StartTime = new TimeOnly(10, 30);
                break;
            case "missingVet":
                request.VeterinarianId = Guid.Empty;
                break;
        }

        await Assert.ThrowsAsync<FluentValidation.ValidationException>(
            () => service.CreateAsync(request));
        Assert.False(await context.ConsultationRequests.AnyAsync());
    }

    // 8. The online path stays marked Source="Online".
    [Fact]
    public async Task OnlineCreateAsync_RemainsOnlineSource()
    {
        using var context = GetInMemoryDbContext();
        await SeedOrgOwnerPet(context);
        var tenant = ScopedManager(OrgA);
        var consultations = new ConsultationRequestService(context, tenant);

        var dto = await consultations.CreateAsync(new CreateConsultationRequestDto
        {
            PetId = "PET-1",
            OwnerId = "OWN-1",
            Symptoms = "Sneezing",
            OrganizationId = OrgA,
            PreferredDate = FutureDate.ToDateTime(TenAM),
            PreferredTime = TenAM.ToTimeSpan()
        });

        Assert.Equal("Online", dto.Source);
    }
}
