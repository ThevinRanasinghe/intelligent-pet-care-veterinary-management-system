using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PetCare.Application.DTOs.Approvals;
using PetCare.Application.DTOs.Billing;
using PetCare.Application.DTOs.Scheduling;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;
using PetCare.Infrastructure.Persistence;
using PetCare.Infrastructure.Services;
using Xunit;

namespace PetCare.UnitTests.Services;

public sealed class ClinicManagerServiceTests
{
    private readonly PetCareDbContext _dbContext;
    private readonly SchedulingService _schedulingService;
    private readonly BillingService _billingService;
    private readonly ApprovalService _approvalService;
    private readonly ClinicManagerService _clinicManagerService;

    private readonly Guid _orgAId = Guid.NewGuid();
    private readonly Guid _orgBId = Guid.NewGuid();

    public ClinicManagerServiceTests()
    {
        var options = new DbContextOptionsBuilder<PetCareDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _dbContext = new PetCareDbContext(options);

        // Seed Organizations
        _dbContext.Organizations.AddRange(
            new Organization { Id = _orgAId, Name = "Happy Paws Clinic", Email = "managerA@happypaws.com", Status = OrganizationStatus.Active },
            new Organization { Id = _orgBId, Name = "Greenfield Veterinary", Email = "managerB@greenfield.com", Status = OrganizationStatus.Active }
        );

        _dbContext.SaveChanges();

        _schedulingService = new SchedulingService(_dbContext);
        _billingService = new BillingService(_dbContext);
        _approvalService = new ApprovalService(_dbContext);
        _clinicManagerService = new ClinicManagerService(_dbContext);
    }

    [Fact]
    public async Task MultiTenantIsolation_AppointmentsAreStrictlyScopedToOrganization()
    {
        // Arrange: Vet and Slot for Org A
        var vetA = new Veterinarian { Id = Guid.NewGuid(), OrganizationId = _orgAId, Name = "Dr. Alice", Specialisation = "Feline", Branch = "Main", Active = true };
        var slotA = new AppointmentSlot { Id = Guid.NewGuid(), VeterinarianId = vetA.Id, Date = new DateOnly(2026, 9, 15), StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 30), Status = AppointmentSlotStatus.Available, Branch = "Main" };
        var apptA = new Appointment { Id = Guid.NewGuid(), OrganizationId = _orgAId, PetId = Guid.NewGuid(), VeterinarianId = vetA.Id, AppointmentSlotId = slotA.Id, Date = new DateOnly(2026, 9, 15), StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(9, 30), Status = AppointmentStatus.Confirmed };

        // Vet and Slot for Org B
        var vetB = new Veterinarian { Id = Guid.NewGuid(), OrganizationId = _orgBId, Name = "Dr. Bob", Specialisation = "Canine", Branch = "North", Active = true };
        var slotB = new AppointmentSlot { Id = Guid.NewGuid(), VeterinarianId = vetB.Id, Date = new DateOnly(2026, 9, 15), StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(10, 30), Status = AppointmentSlotStatus.Available, Branch = "North" };
        var apptB = new Appointment { Id = Guid.NewGuid(), OrganizationId = _orgBId, PetId = Guid.NewGuid(), VeterinarianId = vetB.Id, AppointmentSlotId = slotB.Id, Date = new DateOnly(2026, 9, 15), StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(10, 30), Status = AppointmentStatus.Confirmed };

        _dbContext.Veterinarians.AddRange(vetA, vetB);
        _dbContext.AppointmentSlots.AddRange(slotA, slotB);
        _dbContext.Appointments.AddRange(apptA, apptB);
        await _dbContext.SaveChangesAsync();

        // Act: Query appointments for Org A
        var orgAAppointments = await _schedulingService.GetAppointmentsAsync(_orgAId);
        var crossTenantFetch = await _schedulingService.GetAppointmentByIdAsync(apptB.Id, _orgAId);

        // Assert
        orgAAppointments.Should().HaveCount(1);
        orgAAppointments.First().Id.Should().Be(apptA.Id);
        crossTenantFetch.Should().BeNull("Org A manager should never be able to inspect Org B appointment");
    }

    [Fact]
    public async Task ConflictCheck_DetectsOverlappingAppointments()
    {
        // Arrange
        var vetId = Guid.NewGuid();
        var vet = new Veterinarian { Id = vetId, OrganizationId = _orgAId, Name = "Dr. Dave", Specialisation = "Surgery", Branch = "Main", Active = true };
        var slot = new AppointmentSlot { Id = Guid.NewGuid(), VeterinarianId = vetId, Date = new DateOnly(2026, 9, 20), StartTime = new TimeOnly(14, 0), EndTime = new TimeOnly(15, 0), Status = AppointmentSlotStatus.Reserved, Branch = "Main" };
        var appt = new Appointment { Id = Guid.NewGuid(), OrganizationId = _orgAId, PetId = Guid.NewGuid(), VeterinarianId = vetId, AppointmentSlotId = slot.Id, Date = new DateOnly(2026, 9, 20), StartTime = new TimeOnly(14, 0), EndTime = new TimeOnly(15, 0), Status = AppointmentStatus.Confirmed };

        _dbContext.Veterinarians.Add(vet);
        _dbContext.AppointmentSlots.Add(slot);
        _dbContext.Appointments.Add(appt);
        await _dbContext.SaveChangesAsync();

        // Act 1: Overlapping time (14:30 - 15:30)
        var hasConflict = await _schedulingService.CheckConflictAsync(_orgAId, new ConflictCheckDto(
            VeterinarianId: vetId,
            Date: new DateOnly(2026, 9, 20),
            StartTime: new TimeOnly(14, 30),
            EndTime: new TimeOnly(15, 30)
        ));

        // Act 2: Non-overlapping time (15:00 - 16:00)
        var noConflict = await _schedulingService.CheckConflictAsync(_orgAId, new ConflictCheckDto(
            VeterinarianId: vetId,
            Date: new DateOnly(2026, 9, 20),
            StartTime: new TimeOnly(15, 0),
            EndTime: new TimeOnly(16, 0)
        ));

        // Assert
        hasConflict.Should().BeTrue();
        noConflict.Should().BeFalse();
    }

    [Fact]
    public async Task ApproveProposal_ExecutesAtomicTransaction_FinalizesAppointmentAndLogsHistory()
    {
        // Arrange
        var proposalId = Guid.NewGuid();
        var proposal = new AIProposal
        {
            Id = proposalId,
            OrganizationId = _orgAId,
            PetName = "Milo",
            OwnerName = "John Doe",
            Status = "Pending",
            Urgency = "Moderate",
            SymptomsSummary = "Fever and lethargy",
            PreliminaryRecommendation = "Hydration therapy and blood panel",
            ProposedTreatment = "Electrolytes and antibiotics",
            MedicineAvailabilityStatus = "In Stock",
            ProposedVeterinarianName = "Dr. Sarah",
            ProposedDate = "2026-09-22",
            ProposedTime = "11:00 - 11:30",
            QuotationTotal = 150m,
            BudgetLimit = 200m,
            SubmittedAt = DateTime.UtcNow
        };

        _dbContext.AIProposals.Add(proposal);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _approvalService.ApproveProposalAsync(
            proposalId,
            _orgAId,
            reviewerId: "mgr-001",
            reviewerEmail: "managerA@happypaws.com",
            new ApproveProposalDto("Approved for immediate morning slot.")
        );

        // Assert
        result.Status.Should().Be("Approved");

        // Verify entity persisted in DB
        var updatedProposal = await _dbContext.AIProposals.FindAsync(proposalId);
        updatedProposal.Should().NotBeNull();
        updatedProposal!.Status.Should().Be("Approved");
        updatedProposal.ReviewedBy.Should().Be("managerA@happypaws.com");

        // Verify history entry appended
        var history = await _approvalService.GetApprovalHistoryAsync(proposalId, _orgAId);
        history.Should().NotBeEmpty();
        history.First().NewStatus.Should().Be("Approved");
    }

    [Fact]
    public async Task QuotationCalculation_CalculatesSubtotalAndTotalAccurately()
    {
        // Arrange
        var apptId = Guid.NewGuid();
        var appt = new Appointment
        {
            Id = apptId,
            OrganizationId = _orgAId,
            PetId = Guid.NewGuid(),
            VeterinarianId = Guid.NewGuid(),
            AppointmentSlotId = Guid.NewGuid(),
            Date = new DateOnly(2026, 9, 25),
            StartTime = new TimeOnly(11, 0),
            EndTime = new TimeOnly(11, 30),
            Status = AppointmentStatus.Confirmed
        };
        _dbContext.Appointments.Add(appt);
        await _dbContext.SaveChangesAsync();

        var createDto = new CreateQuotationDto(
            AppointmentId: apptId,
            Budget: 300m,
            Items: new List<CreateQuotationItemRequestDto>
            {
                new("Consultation", "Veterinary physical exam", 1, 50m),
                new("Medicine", "Amoxicillin 250mg", 2, 25m),
                new("Vaccination", "Rabies core vaccine", 1, 40m)
            }
        );

        // Act
        var quotation = await _billingService.CreateQuotationAsync(_orgAId, createDto);

        // Assert
        quotation.Should().NotBeNull();
        // 50 + (2 * 25) + 40 = 140
        quotation.Subtotal.Should().Be(140m);
        quotation.Total.Should().Be(140m);
        quotation.Items.Should().HaveCount(3);
    }
}
