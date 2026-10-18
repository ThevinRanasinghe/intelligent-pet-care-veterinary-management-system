using FluentValidation;
using FluentValidation.Results;
using Moq;
using PetCare.Application.DTOs.Pets;
using PetCare.Application.DTOs.Scheduling;
using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;
using PetCare.Application.Services;
using PetCare.Domain.Constants;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;
using Xunit;

namespace PetCare.Application.Tests.Services;

/// <summary>
/// Unit tests for SchedulingService: appointment creation business rules and
/// the veterinarian overlap/conflict check
/// (NewStart &lt; ExistingEnd AND NewEnd &gt; ExistingStart).
/// All dependencies (repositories, unit of work, validators) are mocked so
/// these tests exercise only the Application layer, per
/// docs/database/scheduling-billing-approval-domain-model.md business rules.
/// </summary>
public class SchedulingServiceTests
{
    private readonly Mock<IAppointmentRepository> _appointmentRepository = new();
    private readonly Mock<IAppointmentSlotRepository> _appointmentSlotRepository = new();
    private readonly Mock<IVeterinarianRepository> _veterinarianRepository = new();
    private readonly Mock<IPetService> _petService = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IValidator<CreateAppointmentRequest>> _createValidator = new();
    private readonly Mock<IValidator<UpdateAppointmentRequest>> _updateValidator = new();
    private readonly Mock<ICurrentVeterinarianResolver> _veterinarianResolver = new();
    private readonly Mock<IOwnerAccessService> _ownerAccess = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Mock<IConsultationRequestRepository> _consultationRequests = new();

    private static readonly Guid VeterinarianId = Guid.NewGuid();
    private static readonly Guid OtherVeterinarianId = Guid.NewGuid();
    private static readonly Guid SlotId = Guid.NewGuid();
    private static readonly string PetId = "PET-TEST0001";
    private static readonly DateOnly Date = new(2025, 6, 2);

    public SchedulingServiceTests()
    {
        // Structural validation always passes; SchedulingService's own business
        // rules are what these tests target.
        _createValidator
            .Setup(v => v.ValidateAsync(It.IsAny<CreateAppointmentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _updateValidator
            .Setup(v => v.ValidateAsync(It.IsAny<UpdateAppointmentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        // Default: the referenced pet exists. Individual tests can override.
        _petService
            .Setup(s => s.GetByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(new PetDto { Id = PetId, OwnerId = "OWN-TEST001", Name = "Test Pet", Species = "Dog" });
    }

    private SchedulingService CreateService() => new(
        _appointmentRepository.Object,
        _appointmentSlotRepository.Object,
        _veterinarianRepository.Object,
        _petService.Object,
        _unitOfWork.Object,
        _createValidator.Object,
        _updateValidator.Object,
        _veterinarianResolver.Object,
        _ownerAccess.Object,
        _tenant.Object,
        _consultationRequests.Object);

    private static Veterinarian ActiveVeterinarian(Guid? id = null) => new()
    {
        Id = id ?? VeterinarianId,
        Name = "Dr. Test Vet",
        Specialisation = "General",
        Branch = "Colombo",
        Active = true
    };

    private static AppointmentSlot Slot(
        Guid veterinarianId,
        TimeOnly start,
        TimeOnly end,
        Guid? id = null) => new()
    {
        Id = id ?? SlotId,
        VeterinarianId = veterinarianId,
        Date = Date,
        StartTime = start,
        EndTime = end,
        Branch = "Colombo",
        Status = AppointmentSlotStatus.Available
    };

    private static Appointment ExistingAppointment(
        Guid veterinarianId,
        TimeOnly start,
        TimeOnly end,
        AppointmentStatus status = AppointmentStatus.Confirmed) => new()
    {
        Id = Guid.NewGuid(),
        PetId = PetId,
        VeterinarianId = veterinarianId,
        AppointmentSlotId = Guid.NewGuid(),
        Date = Date,
        StartTime = start,
        EndTime = end,
        Status = status
    };

    private static CreateAppointmentRequest Request(
        Guid veterinarianId,
        Guid appointmentSlotId,
        TimeOnly start,
        TimeOnly end) => new()
    {
        PetId = PetId,
        VeterinarianId = veterinarianId,
        AppointmentSlotId = appointmentSlotId,
        ScheduledStart = Date.ToDateTime(start),
        ScheduledEnd = Date.ToDateTime(end)
    };

    /// <summary>
    /// Every appointment is consultation-driven: links the request to an
    /// eligible Submitted consultation for the test pet and wires the repo
    /// mock. Consultation carries no OrganizationId, so the org rule passes
    /// regardless of the veterinarian's organization.
    /// </summary>
    private ConsultationRequest LinkConsultation(
        CreateAppointmentRequest request, string id = "CON-LINK0001")
    {
        request.ConsultationRequestId = id;
        var consultation = new ConsultationRequest
        {
            Id = id,
            PetId = PetId,
            OwnerId = "OWN-TEST001",
            SymptomsDescription = "Test symptoms",
            Status = "Submitted"
        };
        _consultationRequests
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(consultation);
        return consultation;
    }

    private void SetUpNoExistingAppointments(Guid veterinarianId)
    {
        _appointmentRepository
            .Setup(r => r.GetActiveByVeterinarianAndDateAsync(veterinarianId, Date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Appointment>());
    }

    private void SetUpExistingAppointments(Guid veterinarianId, params Appointment[] appointments)
    {
        _appointmentRepository
            .Setup(r => r.GetActiveByVeterinarianAndDateAsync(veterinarianId, Date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointments.ToList());
    }

    // 1. Creating an appointment when no conflict exists -> succeeds.
    [Fact]
    public async Task CreateAppointmentAsync_NoConflict_Succeeds()
    {
        var vet = ActiveVeterinarian();
        var slot = Slot(VeterinarianId, new TimeOnly(10, 0), new TimeOnly(11, 0));
        var request = Request(VeterinarianId, slot.Id, new TimeOnly(10, 0), new TimeOnly(11, 0));
        var consultation = LinkConsultation(request);

        _veterinarianRepository.Setup(r => r.GetByIdAsync(VeterinarianId, It.IsAny<CancellationToken>())).ReturnsAsync(vet);
        _appointmentSlotRepository.Setup(r => r.GetByIdAsync(slot.Id, It.IsAny<CancellationToken>())).ReturnsAsync(slot);
        SetUpNoExistingAppointments(VeterinarianId);

        var service = CreateService();

        var result = await service.CreateAppointmentAsync(request);

        Assert.Equal(AppointmentStatus.Confirmed.ToString(), result.Status);
        Assert.Equal("AppointmentConfirmed", consultation.Status);
        _appointmentRepository.Verify(r => r.AddAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // 1b. Link-less creates are rejected — no orphan appointments.
    [Fact]
    public async Task CreateAppointmentAsync_MissingConsultationLink_Throws()
    {
        var vet = ActiveVeterinarian();
        var slot = Slot(VeterinarianId, new TimeOnly(10, 0), new TimeOnly(11, 0));
        var request = Request(VeterinarianId, slot.Id, new TimeOnly(10, 0), new TimeOnly(11, 0));

        _veterinarianRepository.Setup(r => r.GetByIdAsync(VeterinarianId, It.IsAny<CancellationToken>())).ReturnsAsync(vet);
        _appointmentSlotRepository.Setup(r => r.GetByIdAsync(slot.Id, It.IsAny<CancellationToken>())).ReturnsAsync(slot);
        SetUpNoExistingAppointments(VeterinarianId);

        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAppointmentAsync(request));
        _appointmentRepository.Verify(r => r.AddAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(AppointmentSlotStatus.Available, slot.Status);
    }

    // 2. Same veterinarian + same time -> conflict.
    [Fact]
    public async Task CreateAppointmentAsync_SameVeterinarianSameTime_Conflict()
    {
        var vet = ActiveVeterinarian();
        var slot = Slot(VeterinarianId, new TimeOnly(10, 0), new TimeOnly(11, 0));
        var request = Request(VeterinarianId, slot.Id, new TimeOnly(10, 0), new TimeOnly(11, 0));
        var existing = ExistingAppointment(VeterinarianId, new TimeOnly(10, 0), new TimeOnly(11, 0));

        _veterinarianRepository.Setup(r => r.GetByIdAsync(VeterinarianId, It.IsAny<CancellationToken>())).ReturnsAsync(vet);
        _appointmentSlotRepository.Setup(r => r.GetByIdAsync(slot.Id, It.IsAny<CancellationToken>())).ReturnsAsync(slot);
        SetUpExistingAppointments(VeterinarianId, existing);

        var service = CreateService();

        await Assert.ThrowsAsync<SchedulingConflictException>(() => service.CreateAppointmentAsync(request));
    }

    // 3. New appointment starts during an existing appointment -> conflict.
    [Fact]
    public async Task CreateAppointmentAsync_NewStartsDuringExisting_Conflict()
    {
        var vet = ActiveVeterinarian();
        // Existing 10:00-11:00, slot wide enough to host the new 10:30-11:30 request.
        var slot = Slot(VeterinarianId, new TimeOnly(9, 0), new TimeOnly(12, 0));
        var request = Request(VeterinarianId, slot.Id, new TimeOnly(10, 30), new TimeOnly(11, 30));
        var existing = ExistingAppointment(VeterinarianId, new TimeOnly(10, 0), new TimeOnly(11, 0));

        _veterinarianRepository.Setup(r => r.GetByIdAsync(VeterinarianId, It.IsAny<CancellationToken>())).ReturnsAsync(vet);
        _appointmentSlotRepository.Setup(r => r.GetByIdAsync(slot.Id, It.IsAny<CancellationToken>())).ReturnsAsync(slot);
        SetUpExistingAppointments(VeterinarianId, existing);

        var service = CreateService();

        await Assert.ThrowsAsync<SchedulingConflictException>(() => service.CreateAppointmentAsync(request));
    }

    // 4. New appointment ends during an existing appointment -> conflict.
    [Fact]
    public async Task CreateAppointmentAsync_NewEndsDuringExisting_Conflict()
    {
        var vet = ActiveVeterinarian();
        var slot = Slot(VeterinarianId, new TimeOnly(9, 0), new TimeOnly(12, 0));
        var request = Request(VeterinarianId, slot.Id, new TimeOnly(9, 30), new TimeOnly(10, 30));
        var existing = ExistingAppointment(VeterinarianId, new TimeOnly(10, 0), new TimeOnly(11, 0));

        _veterinarianRepository.Setup(r => r.GetByIdAsync(VeterinarianId, It.IsAny<CancellationToken>())).ReturnsAsync(vet);
        _appointmentSlotRepository.Setup(r => r.GetByIdAsync(slot.Id, It.IsAny<CancellationToken>())).ReturnsAsync(slot);
        SetUpExistingAppointments(VeterinarianId, existing);

        var service = CreateService();

        await Assert.ThrowsAsync<SchedulingConflictException>(() => service.CreateAppointmentAsync(request));
    }

    // 5. New appointment completely surrounds an existing appointment -> conflict.
    [Fact]
    public async Task CreateAppointmentAsync_NewSurroundsExisting_Conflict()
    {
        var vet = ActiveVeterinarian();
        var slot = Slot(VeterinarianId, new TimeOnly(9, 0), new TimeOnly(12, 0));
        var request = Request(VeterinarianId, slot.Id, new TimeOnly(10, 0), new TimeOnly(11, 0));
        var existing = ExistingAppointment(VeterinarianId, new TimeOnly(10, 15), new TimeOnly(10, 45));

        _veterinarianRepository.Setup(r => r.GetByIdAsync(VeterinarianId, It.IsAny<CancellationToken>())).ReturnsAsync(vet);
        _appointmentSlotRepository.Setup(r => r.GetByIdAsync(slot.Id, It.IsAny<CancellationToken>())).ReturnsAsync(slot);
        SetUpExistingAppointments(VeterinarianId, existing);

        var service = CreateService();

        await Assert.ThrowsAsync<SchedulingConflictException>(() => service.CreateAppointmentAsync(request));
    }

    // 6. New appointment starts exactly when an existing appointment ends -> allowed.
    [Fact]
    public async Task CreateAppointmentAsync_NewStartsExactlyWhenExistingEnds_Allowed()
    {
        var vet = ActiveVeterinarian();
        var slot = Slot(VeterinarianId, new TimeOnly(9, 0), new TimeOnly(13, 0));
        var request = Request(VeterinarianId, slot.Id, new TimeOnly(11, 0), new TimeOnly(12, 0));
        var existing = ExistingAppointment(VeterinarianId, new TimeOnly(10, 0), new TimeOnly(11, 0));
        LinkConsultation(request);

        _veterinarianRepository.Setup(r => r.GetByIdAsync(VeterinarianId, It.IsAny<CancellationToken>())).ReturnsAsync(vet);
        _appointmentSlotRepository.Setup(r => r.GetByIdAsync(slot.Id, It.IsAny<CancellationToken>())).ReturnsAsync(slot);
        SetUpExistingAppointments(VeterinarianId, existing);

        var service = CreateService();

        var result = await service.CreateAppointmentAsync(request);

        Assert.Equal(AppointmentStatus.Confirmed.ToString(), result.Status);
    }

    // 7. Different veterinarian at the same time -> allowed.
    [Fact]
    public async Task CreateAppointmentAsync_DifferentVeterinarianSameTime_Allowed()
    {
        var vet = ActiveVeterinarian(OtherVeterinarianId);
        var slot = Slot(OtherVeterinarianId, new TimeOnly(10, 0), new TimeOnly(11, 0));
        var request = Request(OtherVeterinarianId, slot.Id, new TimeOnly(10, 0), new TimeOnly(11, 0));
        LinkConsultation(request);

        // Existing appointment belongs to a different veterinarian (VeterinarianId),
        // so the "other" veterinarian's day has no active appointments.
        _veterinarianRepository.Setup(r => r.GetByIdAsync(OtherVeterinarianId, It.IsAny<CancellationToken>())).ReturnsAsync(vet);
        _appointmentSlotRepository.Setup(r => r.GetByIdAsync(slot.Id, It.IsAny<CancellationToken>())).ReturnsAsync(slot);
        SetUpNoExistingAppointments(OtherVeterinarianId);

        var service = CreateService();

        var result = await service.CreateAppointmentAsync(request);

        Assert.Equal(AppointmentStatus.Confirmed.ToString(), result.Status);
    }

    // 8. Cancelled appointment does not cause a conflict.
    [Fact]
    public async Task CreateAppointmentAsync_OverlappingCancelledAppointment_Allowed()
    {
        var vet = ActiveVeterinarian();
        var slot = Slot(VeterinarianId, new TimeOnly(9, 0), new TimeOnly(12, 0));
        var request = Request(VeterinarianId, slot.Id, new TimeOnly(10, 0), new TimeOnly(11, 0));
        var cancelled = ExistingAppointment(VeterinarianId, new TimeOnly(10, 0), new TimeOnly(11, 0), AppointmentStatus.Cancelled);
        LinkConsultation(request);

        _veterinarianRepository.Setup(r => r.GetByIdAsync(VeterinarianId, It.IsAny<CancellationToken>())).ReturnsAsync(vet);
        _appointmentSlotRepository.Setup(r => r.GetByIdAsync(slot.Id, It.IsAny<CancellationToken>())).ReturnsAsync(slot);
        SetUpExistingAppointments(VeterinarianId, cancelled);

        var service = CreateService();

        var result = await service.CreateAppointmentAsync(request);

        Assert.Equal(AppointmentStatus.Confirmed.ToString(), result.Status);
    }

    // 9. Inactive veterinarian cannot receive an appointment.
    [Fact]
    public async Task CreateAppointmentAsync_InactiveVeterinarian_ThrowsConflict()
    {
        var inactiveVet = ActiveVeterinarian();
        inactiveVet.Active = false;
        var slot = Slot(VeterinarianId, new TimeOnly(10, 0), new TimeOnly(11, 0));
        var request = Request(VeterinarianId, slot.Id, new TimeOnly(10, 0), new TimeOnly(11, 0));

        _veterinarianRepository.Setup(r => r.GetByIdAsync(VeterinarianId, It.IsAny<CancellationToken>())).ReturnsAsync(inactiveVet);
        _appointmentSlotRepository.Setup(r => r.GetByIdAsync(slot.Id, It.IsAny<CancellationToken>())).ReturnsAsync(slot);

        var service = CreateService();

        await Assert.ThrowsAsync<SchedulingConflictException>(() => service.CreateAppointmentAsync(request));

        _appointmentRepository.Verify(r => r.AddAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // 10b. Referencing a non-existent pet is rejected (canonical Pets FK).
    [Fact]
    public async Task CreateAppointmentAsync_UnknownPet_ThrowsNotFound()
    {
        var request = Request(VeterinarianId, SlotId, new TimeOnly(10, 0), new TimeOnly(11, 0));

        _petService
            .Setup(s => s.GetByIdAsync(request.PetId))
            .ReturnsAsync((PetDto?)null);

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateAppointmentAsync(request));

        _appointmentRepository.Verify(r => r.AddAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // 11. Appointment outside the selected slot is rejected.
    [Fact]
    public async Task CreateAppointmentAsync_OutsideSelectedSlot_ThrowsConflict()
    {
        var vet = ActiveVeterinarian();
        var slot = Slot(VeterinarianId, new TimeOnly(10, 0), new TimeOnly(10, 30));
        // Requested time extends past the slot's end (10:30).
        var request = Request(VeterinarianId, slot.Id, new TimeOnly(10, 0), new TimeOnly(11, 0));

        _veterinarianRepository.Setup(r => r.GetByIdAsync(VeterinarianId, It.IsAny<CancellationToken>())).ReturnsAsync(vet);
        _appointmentSlotRepository.Setup(r => r.GetByIdAsync(slot.Id, It.IsAny<CancellationToken>())).ReturnsAsync(slot);

        var service = CreateService();

        await Assert.ThrowsAsync<SchedulingConflictException>(() => service.CreateAppointmentAsync(request));

        _appointmentRepository.Verify(r => r.AddAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // 12. GET /appointments/mine for a veterinarian filters by the vet
    //     profile resolved from the caller's account — never client input.
    [Fact]
    public async Task GetMyAppointmentsAsync_Veterinarian_UsesResolvedVeterinarianId()
    {
        var vet = ActiveVeterinarian();
        vet.Id = VeterinarianId;
        _tenant.Setup(t => t.IsInRole(Roles.Veterinarian)).Returns(true);
        _veterinarianResolver.Setup(r => r.ResolveRequiredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(vet);
        _appointmentRepository
            .Setup(r => r.GetByVeterinarianAsync(VeterinarianId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Appointment>
            {
                ExistingAppointment(VeterinarianId, new TimeOnly(10, 0), new TimeOnly(11, 0))
            });

        var service = CreateService();
        var result = await service.GetMyAppointmentsAsync(null, null, null, null);

        Assert.Single(result);
        _appointmentRepository.Verify(r => r.GetByVeterinarianAsync(VeterinarianId, It.IsAny<CancellationToken>()), Times.Once);
        _appointmentRepository.Verify(r => r.GetByOwnerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _appointmentRepository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // 13. A Veterinarian-role user without a linked profile is forbidden.
    [Fact]
    public async Task GetMyAppointmentsAsync_UnlinkedVeterinarian_ThrowsForbidden()
    {
        _tenant.Setup(t => t.IsInRole(Roles.Veterinarian)).Returns(true);
        _veterinarianResolver.Setup(r => r.ResolveRequiredAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ForbiddenException("Your account is not linked to a veterinarian profile."));

        var service = CreateService();

        await Assert.ThrowsAsync<ForbiddenException>(
            () => service.GetMyAppointmentsAsync(null, null, null, null));
        _appointmentRepository.Verify(r => r.GetByVeterinarianAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // 14. GET /appointments/mine for a pet owner filters by the caller's
    //     owner id (ownership path, not org scope).
    [Fact]
    public async Task GetMyAppointmentsAsync_PetOwner_UsesOwnerId()
    {
        _tenant.Setup(t => t.IsInRole(It.IsAny<string>())).Returns(false);
        _ownerAccess.Setup(o => o.IsPetOwner).Returns(true);
        _ownerAccess.Setup(o => o.GetOwnerIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync("OWN-TEST0001");
        _appointmentRepository
            .Setup(r => r.GetByOwnerAsync("OWN-TEST0001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Appointment>
            {
                ExistingAppointment(VeterinarianId, new TimeOnly(10, 0), new TimeOnly(11, 0))
            });

        var service = CreateService();
        var result = await service.GetMyAppointmentsAsync(null, null, null, null);

        Assert.Single(result);
        _appointmentRepository.Verify(r => r.GetByOwnerAsync("OWN-TEST0001", It.IsAny<CancellationToken>()), Times.Once);
        _appointmentRepository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // 15. Staff callers (manager/admin) get the org-scoped list.
    [Fact]
    public async Task GetMyAppointmentsAsync_Manager_ReturnsOrgList()
    {
        _tenant.Setup(t => t.IsInRole(Roles.Veterinarian)).Returns(false);
        _ownerAccess.Setup(o => o.IsPetOwner).Returns(false);
        _appointmentRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Appointment>
            {
                ExistingAppointment(VeterinarianId, new TimeOnly(10, 0), new TimeOnly(11, 0)),
                ExistingAppointment(VeterinarianId, new TimeOnly(13, 0), new TimeOnly(14, 0))
            });

        var service = CreateService();
        var result = await service.GetMyAppointmentsAsync(null, null, null, null);

        Assert.Equal(2, result.Count);
        _appointmentRepository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------------------------------------------------------------
    // UpdateAppointmentAsync — reschedule / veterinarian reassignment
    // ---------------------------------------------------------------

    private static readonly DateOnly FutureDate =
        DateOnly.FromDateTime(DateTime.Today.AddDays(6));

    private Appointment BookedAppointment(
        Guid veterinarianId,
        Guid slotId,
        DateOnly date,
        TimeOnly start,
        TimeOnly end,
        string? consultationId = null) => new()
    {
        Id = Guid.NewGuid(),
        PetId = PetId,
        VeterinarianId = veterinarianId,
        AppointmentSlotId = slotId,
        Date = date,
        StartTime = start,
        EndTime = end,
        Status = AppointmentStatus.Confirmed,
        ConsultationRequestId = consultationId
    };

    private Mock<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> SetupTransaction()
    {
        var tx = new Mock<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>();
        _unitOfWork
            .Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tx.Object);
        return tx;
    }

    // 16. A notes-only edit touches no slots and performs a single save.
    [Fact]
    public async Task UpdateAppointmentAsync_NotesOnly_SkipsSlotWork()
    {
        var slotId = Guid.NewGuid();
        var appointment = BookedAppointment(
            VeterinarianId, slotId, Date, new TimeOnly(10, 0), new TimeOnly(11, 0));
        _appointmentRepository
            .Setup(r => r.GetByIdAsync(appointment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);

        var service = CreateService();
        await service.UpdateAppointmentAsync(appointment.Id, new UpdateAppointmentRequest
        {
            ScheduledStart = Date.ToDateTime(new TimeOnly(10, 0)),
            ScheduledEnd = Date.ToDateTime(new TimeOnly(11, 0)),
            Notes = "Bring vaccination card"
        });

        Assert.Equal("Bring vaccination card", appointment.Notes);
        _veterinarianRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _appointmentSlotRepository.Verify(
            r => r.GetByVeterinarianDateStartAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<TimeOnly>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // 17. Reassigning the veterinarian releases the old slot and reserves
    //     a new one for the target vet — in a transaction.
    [Fact]
    public async Task UpdateAppointmentAsync_ReassignVeterinarian_ReleasesOldSlotAndReservesNew()
    {
        var oldSlot = Slot(VeterinarianId, new TimeOnly(10, 0), new TimeOnly(11, 0));
        oldSlot.Status = AppointmentSlotStatus.Reserved;
        var appointment = BookedAppointment(
            VeterinarianId, oldSlot.Id, Date, new TimeOnly(10, 0), new TimeOnly(11, 0));

        _appointmentRepository
            .Setup(r => r.GetByIdAsync(appointment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);
        _veterinarianRepository
            .Setup(r => r.GetByIdAsync(OtherVeterinarianId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveVeterinarian(OtherVeterinarianId));
        _appointmentRepository
            .Setup(r => r.GetActiveByVeterinarianAndDateAsync(
                OtherVeterinarianId, FutureDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Appointment>());
        _appointmentSlotRepository
            .Setup(r => r.GetByVeterinarianDateStartAsync(
                OtherVeterinarianId, FutureDate, new TimeOnly(14, 0), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppointmentSlot?)null);
        _appointmentSlotRepository
            .Setup(r => r.GetByStatusInWindowAsync(
                VeterinarianId, Date, new TimeOnly(10, 0), new TimeOnly(11, 0),
                AppointmentSlotStatus.Reserved, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AppointmentSlot> { oldSlot });
        var tx = SetupTransaction();

        var service = CreateService();
        var result = await service.UpdateAppointmentAsync(appointment.Id, new UpdateAppointmentRequest
        {
            VeterinarianId = OtherVeterinarianId,
            ScheduledStart = FutureDate.ToDateTime(new TimeOnly(14, 0)),
            ScheduledEnd = FutureDate.ToDateTime(new TimeOnly(15, 0)),
            Notes = "Reassigned"
        });

        Assert.Equal(AppointmentSlotStatus.Available, oldSlot.Status);
        _appointmentSlotRepository.Verify(s => s.AddAsync(
            It.Is<AppointmentSlot>(x =>
                x.VeterinarianId == OtherVeterinarianId &&
                x.Date == FutureDate &&
                x.StartTime == new TimeOnly(14, 0) &&
                x.EndTime == new TimeOnly(15, 0) &&
                x.Status == AppointmentSlotStatus.Reserved),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(OtherVeterinarianId, appointment.VeterinarianId);
        Assert.Equal(FutureDate, appointment.Date);
        tx.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(AppointmentStatus.Confirmed.ToString(), result.Status);
    }

    // 18. Rescheduling onto a pre-posted Available slot adopts it.
    [Fact]
    public async Task UpdateAppointmentAsync_Reschedule_AdoptsPostedSlot()
    {
        var oldSlot = Slot(VeterinarianId, new TimeOnly(10, 0), new TimeOnly(11, 0));
        oldSlot.Status = AppointmentSlotStatus.Reserved;
        var appointment = BookedAppointment(
            VeterinarianId, oldSlot.Id, Date, new TimeOnly(10, 0), new TimeOnly(11, 0));
        var posted = new AppointmentSlot
        {
            Id = Guid.NewGuid(),
            VeterinarianId = VeterinarianId,
            Date = FutureDate,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
            Branch = "Colombo",
            Status = AppointmentSlotStatus.Available
        };

        _appointmentRepository
            .Setup(r => r.GetByIdAsync(appointment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);
        _veterinarianRepository
            .Setup(r => r.GetByIdAsync(VeterinarianId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveVeterinarian());
        _appointmentRepository
            .Setup(r => r.GetActiveByVeterinarianAndDateAsync(
                VeterinarianId, FutureDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Appointment>());
        _appointmentSlotRepository
            .Setup(r => r.GetByVeterinarianDateStartAsync(
                VeterinarianId, FutureDate, new TimeOnly(9, 0), It.IsAny<CancellationToken>()))
            .ReturnsAsync(posted);
        _appointmentSlotRepository
            .Setup(r => r.GetByStatusInWindowAsync(
                VeterinarianId, Date, new TimeOnly(10, 0), new TimeOnly(11, 0),
                AppointmentSlotStatus.Reserved, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AppointmentSlot> { oldSlot });
        SetupTransaction();

        var service = CreateService();
        await service.UpdateAppointmentAsync(appointment.Id, new UpdateAppointmentRequest
        {
            ScheduledStart = FutureDate.ToDateTime(new TimeOnly(9, 0)),
            ScheduledEnd = FutureDate.ToDateTime(new TimeOnly(10, 0))
        });

        Assert.Equal(posted.Id, appointment.AppointmentSlotId);
        Assert.Equal(AppointmentSlotStatus.Reserved, posted.Status);
        Assert.Equal(AppointmentSlotStatus.Available, oldSlot.Status);
        _appointmentSlotRepository.Verify(
            s => s.AddAsync(It.IsAny<AppointmentSlot>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // 19. A slot already Reserved by someone else cannot be taken.
    [Fact]
    public async Task UpdateAppointmentAsync_ReservedTargetSlot_ThrowsConflict()
    {
        var oldSlot = Slot(VeterinarianId, new TimeOnly(10, 0), new TimeOnly(11, 0));
        oldSlot.Status = AppointmentSlotStatus.Reserved;
        var appointment = BookedAppointment(
            VeterinarianId, oldSlot.Id, Date, new TimeOnly(10, 0), new TimeOnly(11, 0));
        var taken = new AppointmentSlot
        {
            Id = Guid.NewGuid(),
            VeterinarianId = VeterinarianId,
            Date = FutureDate,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 0),
            Branch = "Colombo",
            Status = AppointmentSlotStatus.Reserved
        };

        _appointmentRepository
            .Setup(r => r.GetByIdAsync(appointment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);
        _veterinarianRepository
            .Setup(r => r.GetByIdAsync(VeterinarianId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveVeterinarian());
        _appointmentRepository
            .Setup(r => r.GetActiveByVeterinarianAndDateAsync(
                VeterinarianId, FutureDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Appointment>());
        _appointmentSlotRepository
            .Setup(r => r.GetByVeterinarianDateStartAsync(
                VeterinarianId, FutureDate, new TimeOnly(9, 0), It.IsAny<CancellationToken>()))
            .ReturnsAsync(taken);
        SetupTransaction();

        var service = CreateService();
        await Assert.ThrowsAsync<SchedulingConflictException>(
            () => service.UpdateAppointmentAsync(appointment.Id, new UpdateAppointmentRequest
            {
                ScheduledStart = FutureDate.ToDateTime(new TimeOnly(9, 0)),
                ScheduledEnd = FutureDate.ToDateTime(new TimeOnly(10, 0))
            }));
    }

    // 20. Reassigning to a veterinarian with a conflicting appointment fails.
    [Fact]
    public async Task UpdateAppointmentAsync_TargetVetBusy_ThrowsConflict()
    {
        var appointment = BookedAppointment(
            VeterinarianId, Guid.NewGuid(), Date, new TimeOnly(10, 0), new TimeOnly(11, 0));
        var conflicting = new Appointment
        {
            Id = Guid.NewGuid(),
            PetId = "PET-OTHER",
            VeterinarianId = OtherVeterinarianId,
            AppointmentSlotId = Guid.NewGuid(),
            Date = FutureDate,
            StartTime = new TimeOnly(14, 0),
            EndTime = new TimeOnly(15, 0),
            Status = AppointmentStatus.Confirmed
        };

        _appointmentRepository
            .Setup(r => r.GetByIdAsync(appointment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);
        _veterinarianRepository
            .Setup(r => r.GetByIdAsync(OtherVeterinarianId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveVeterinarian(OtherVeterinarianId));
        _appointmentRepository
            .Setup(r => r.GetActiveByVeterinarianAndDateAsync(
                OtherVeterinarianId, FutureDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Appointment> { conflicting });

        var service = CreateService();
        await Assert.ThrowsAsync<SchedulingConflictException>(
            () => service.UpdateAppointmentAsync(appointment.Id, new UpdateAppointmentRequest
            {
                VeterinarianId = OtherVeterinarianId,
                ScheduledStart = FutureDate.ToDateTime(new TimeOnly(14, 0)),
                ScheduledEnd = FutureDate.ToDateTime(new TimeOnly(15, 0))
            }));

        _appointmentSlotRepository.Verify(
            s => s.AddAsync(It.IsAny<AppointmentSlot>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // 21. Completed/cancelled appointments are immutable.
    [Theory]
    [InlineData(AppointmentStatus.Completed)]
    [InlineData(AppointmentStatus.Cancelled)]
    public async Task UpdateAppointmentAsync_TerminalStatus_ThrowsConflict(AppointmentStatus status)
    {
        var appointment = BookedAppointment(
            VeterinarianId, Guid.NewGuid(), Date, new TimeOnly(10, 0), new TimeOnly(11, 0));
        appointment.Status = status;
        _appointmentRepository
            .Setup(r => r.GetByIdAsync(appointment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);

        var service = CreateService();
        await Assert.ThrowsAsync<SchedulingConflictException>(
            () => service.UpdateAppointmentAsync(appointment.Id, new UpdateAppointmentRequest
            {
                ScheduledStart = FutureDate.ToDateTime(new TimeOnly(9, 0)),
                ScheduledEnd = FutureDate.ToDateTime(new TimeOnly(10, 0))
            }));
    }

    // 22. The linked consultation follows the new slot: PreferredDate moves
    //     and the change lands in its status history.
    [Fact]
    public async Task UpdateAppointmentAsync_LinkedConsultation_SyncsDateAndHistory()
    {
        var oldSlot = Slot(VeterinarianId, new TimeOnly(10, 0), new TimeOnly(11, 0));
        oldSlot.Status = AppointmentSlotStatus.Reserved;
        var appointment = BookedAppointment(
            VeterinarianId, oldSlot.Id, Date, new TimeOnly(10, 0), new TimeOnly(11, 0),
            consultationId: "CON-1");
        var consultation = new ConsultationRequest
        {
            Id = "CON-1",
            PetId = PetId,
            OwnerId = "OWN-TEST001",
            SymptomsDescription = "Limping",
            PreferredDate = Date.ToDateTime(new TimeOnly(10, 0)),
            Status = "AppointmentConfirmed"
        };

        _appointmentRepository
            .Setup(r => r.GetByIdAsync(appointment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);
        _veterinarianRepository
            .Setup(r => r.GetByIdAsync(OtherVeterinarianId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveVeterinarian(OtherVeterinarianId));
        _appointmentRepository
            .Setup(r => r.GetActiveByVeterinarianAndDateAsync(
                OtherVeterinarianId, FutureDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Appointment>());
        _appointmentSlotRepository
            .Setup(r => r.GetByVeterinarianDateStartAsync(
                OtherVeterinarianId, FutureDate, new TimeOnly(14, 0), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppointmentSlot?)null);
        _appointmentSlotRepository
            .Setup(r => r.GetByStatusInWindowAsync(
                VeterinarianId, Date, new TimeOnly(10, 0), new TimeOnly(11, 0),
                AppointmentSlotStatus.Reserved, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AppointmentSlot> { oldSlot });
        _consultationRequests
            .Setup(r => r.GetByIdAsync("CON-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(consultation);
        SetupTransaction();

        var service = CreateService();
        await service.UpdateAppointmentAsync(appointment.Id, new UpdateAppointmentRequest
        {
            VeterinarianId = OtherVeterinarianId,
            ScheduledStart = FutureDate.ToDateTime(new TimeOnly(14, 0)),
            ScheduledEnd = FutureDate.ToDateTime(new TimeOnly(15, 0))
        });

        Assert.Equal(new TimeOnly(14, 0), TimeOnly.FromDateTime(consultation.PreferredDate));
        Assert.Contains(consultation.StatusHistories,
            h => h.Comments != null && h.Comments.Contains("reassigned"));
    }

    // ---------------------------------------------------------------
    // CreateAppointmentAsync — optional consultation link
    // ---------------------------------------------------------------

    private static readonly Guid OrgId = Guid.NewGuid();

    private void SetupCreatePath(AppointmentSlot slot)
    {
        _veterinarianRepository
            .Setup(r => r.GetByIdAsync(VeterinarianId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveVeterinarian());
        _appointmentSlotRepository
            .Setup(r => r.GetByIdAsync(slot.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);
        SetUpNoExistingAppointments(VeterinarianId);
    }

    // 23. A consultation-linked create produces the assign-path end state:
    //     Confirmed appointment, linked request, confirmed consultation +
    //     status history.
    [Fact]
    public async Task CreateAppointmentAsync_WithConsultationLink_ConfirmsAndLinks()
    {
        var vet = ActiveVeterinarian();
        vet.OrganizationId = OrgId;
        var slot = Slot(VeterinarianId, new TimeOnly(10, 0), new TimeOnly(11, 0));
        var consultation = new ConsultationRequest
        {
            Id = "CON-L1",
            PetId = PetId,
            OwnerId = "OWN-TEST001",
            SymptomsDescription = "Limping",
            OrganizationId = OrgId,
            Status = "Submitted"
        };

        _veterinarianRepository
            .Setup(r => r.GetByIdAsync(VeterinarianId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vet);
        _appointmentSlotRepository
            .Setup(r => r.GetByIdAsync(slot.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);
        SetUpNoExistingAppointments(VeterinarianId);
        _consultationRequests
            .Setup(r => r.GetByIdAsync("CON-L1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(consultation);

        var service = CreateService();
        var request = Request(VeterinarianId, slot.Id, new TimeOnly(10, 0), new TimeOnly(11, 0));
        request.ConsultationRequestId = "CON-L1";

        var result = await service.CreateAppointmentAsync(request);

        Assert.Equal(AppointmentStatus.Confirmed.ToString(), result.Status);
        Assert.Equal("CON-L1", result.ConsultationRequestId);
        Assert.Equal("AppointmentConfirmed", consultation.Status);
        Assert.Contains(consultation.StatusHistories,
            h => h.Status == "AppointmentConfirmed");
    }

    // 24. A link to a consultation for a different pet is rejected.
    [Fact]
    public async Task CreateAppointmentAsync_LinkedConsultationWrongPet_Throws()
    {
        var slot = Slot(VeterinarianId, new TimeOnly(10, 0), new TimeOnly(11, 0));
        var consultation = new ConsultationRequest
        {
            Id = "CON-L2",
            PetId = "PET-OTHER",
            OwnerId = "OWN-OTHER",
            SymptomsDescription = "X",
            Status = "Submitted"
        };

        SetupCreatePath(slot);
        _consultationRequests
            .Setup(r => r.GetByIdAsync("CON-L2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(consultation);

        var service = CreateService();
        var request = Request(VeterinarianId, slot.Id, new TimeOnly(10, 0), new TimeOnly(11, 0));
        request.ConsultationRequestId = "CON-L2";

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateAppointmentAsync(request));
        _appointmentRepository.Verify(
            r => r.AddAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // 25. A link to an already-confirmed/cancelled consultation is rejected —
    //     the request is no longer eligible for assignment.
    [Theory]
    [InlineData("AppointmentConfirmed")]
    [InlineData("Cancelled")]
    [InlineData("Completed")]
    public async Task CreateAppointmentAsync_LinkedConsultationIneligibleStatus_Throws(string status)
    {
        var slot = Slot(VeterinarianId, new TimeOnly(10, 0), new TimeOnly(11, 0));
        var consultation = new ConsultationRequest
        {
            Id = "CON-L3",
            PetId = PetId,
            OwnerId = "OWN-TEST001",
            SymptomsDescription = "X",
            Status = status
        };

        SetupCreatePath(slot);
        _consultationRequests
            .Setup(r => r.GetByIdAsync("CON-L3", It.IsAny<CancellationToken>()))
            .ReturnsAsync(consultation);

        var service = CreateService();
        var request = Request(VeterinarianId, slot.Id, new TimeOnly(10, 0), new TimeOnly(11, 0));
        request.ConsultationRequestId = "CON-L3";

        await Assert.ThrowsAsync<SchedulingConflictException>(
            () => service.CreateAppointmentAsync(request));
        _appointmentRepository.Verify(
            r => r.AddAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // 26. A link to a consultation filed with another organization is
    //     rejected — the vet must belong to that clinic.
    [Fact]
    public async Task CreateAppointmentAsync_LinkedConsultationOtherOrg_Throws()
    {
        var vet = ActiveVeterinarian();
        vet.OrganizationId = OrgId;
        var slot = Slot(VeterinarianId, new TimeOnly(10, 0), new TimeOnly(11, 0));
        var consultation = new ConsultationRequest
        {
            Id = "CON-L4",
            PetId = PetId,
            OwnerId = "OWN-TEST001",
            SymptomsDescription = "X",
            OrganizationId = Guid.NewGuid(), // different clinic
            Status = "Submitted"
        };

        _veterinarianRepository
            .Setup(r => r.GetByIdAsync(VeterinarianId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vet);
        _appointmentSlotRepository
            .Setup(r => r.GetByIdAsync(slot.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slot);
        SetUpNoExistingAppointments(VeterinarianId);
        _consultationRequests
            .Setup(r => r.GetByIdAsync("CON-L4", It.IsAny<CancellationToken>()))
            .ReturnsAsync(consultation);

        var service = CreateService();
        var request = Request(VeterinarianId, slot.Id, new TimeOnly(10, 0), new TimeOnly(11, 0));
        request.ConsultationRequestId = "CON-L4";

        await Assert.ThrowsAsync<SchedulingConflictException>(
            () => service.CreateAppointmentAsync(request));
    }

    // ---------------------------------------------------------------
    // UpdateAppointmentAsync — notes contract + multi-hour windows
    // ---------------------------------------------------------------

    // 27. A vet-only reassignment with no Notes field preserves the note.
    [Fact]
    public async Task UpdateAppointmentAsync_OmittedNotes_PreservesExisting()
    {
        var oldSlot = Slot(VeterinarianId, new TimeOnly(10, 0), new TimeOnly(11, 0));
        oldSlot.Status = AppointmentSlotStatus.Reserved;
        var appointment = BookedAppointment(
            VeterinarianId, oldSlot.Id, Date, new TimeOnly(10, 0), new TimeOnly(11, 0));
        appointment.Notes = "Existing note";

        _appointmentRepository
            .Setup(r => r.GetByIdAsync(appointment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);
        _veterinarianRepository
            .Setup(r => r.GetByIdAsync(OtherVeterinarianId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveVeterinarian(OtherVeterinarianId));
        _appointmentRepository
            .Setup(r => r.GetActiveByVeterinarianAndDateAsync(
                OtherVeterinarianId, FutureDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Appointment>());
        _appointmentSlotRepository
            .Setup(r => r.GetByVeterinarianDateStartAsync(
                OtherVeterinarianId, FutureDate, new TimeOnly(14, 0), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppointmentSlot?)null);
        _appointmentSlotRepository
            .Setup(r => r.GetByStatusInWindowAsync(
                VeterinarianId, Date, new TimeOnly(10, 0), new TimeOnly(11, 0),
                AppointmentSlotStatus.Reserved, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AppointmentSlot> { oldSlot });
        SetupTransaction();

        var service = CreateService();
        await service.UpdateAppointmentAsync(appointment.Id, new UpdateAppointmentRequest
        {
            VeterinarianId = OtherVeterinarianId,
            ScheduledStart = FutureDate.ToDateTime(new TimeOnly(14, 0)),
            ScheduledEnd = FutureDate.ToDateTime(new TimeOnly(15, 0))
            // Notes omitted entirely.
        });

        Assert.Equal("Existing note", appointment.Notes);
    }

    // 28. An explicit empty string clears the note (documented contract).
    [Fact]
    public async Task UpdateAppointmentAsync_EmptyNotes_ClearsNote()
    {
        var appointment = BookedAppointment(
            VeterinarianId, Guid.NewGuid(), Date, new TimeOnly(10, 0), new TimeOnly(11, 0));
        appointment.Notes = "Existing note";
        _appointmentRepository
            .Setup(r => r.GetByIdAsync(appointment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);

        var service = CreateService();
        await service.UpdateAppointmentAsync(appointment.Id, new UpdateAppointmentRequest
        {
            ScheduledStart = Date.ToDateTime(new TimeOnly(10, 0)),
            ScheduledEnd = Date.ToDateTime(new TimeOnly(11, 0)),
            Notes = ""
        });

        Assert.Null(appointment.Notes);
    }

    // 29. Extending 1h -> 2h keeps the anchor and reserves every hour of the
    //     new window (the 11:00 slot is materialized, not just implied).
    [Fact]
    public async Task UpdateAppointmentAsync_ExtendToTwoHours_ReservesEveryHour()
    {
        var anchor = Slot(VeterinarianId, new TimeOnly(10, 0), new TimeOnly(11, 0));
        anchor.Date = FutureDate;
        anchor.Status = AppointmentSlotStatus.Reserved;
        var appointment = BookedAppointment(
            VeterinarianId, anchor.Id, FutureDate, new TimeOnly(10, 0), new TimeOnly(11, 0));

        _appointmentRepository
            .Setup(r => r.GetByIdAsync(appointment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);
        _veterinarianRepository
            .Setup(r => r.GetByIdAsync(VeterinarianId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveVeterinarian());
        _appointmentRepository
            .Setup(r => r.GetActiveByVeterinarianAndDateAsync(
                VeterinarianId, FutureDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Appointment>());
        // Anchor hour resolves to the appointment's own slot; 11:00 is free.
        _appointmentSlotRepository
            .Setup(r => r.GetByVeterinarianDateStartAsync(
                VeterinarianId, FutureDate, new TimeOnly(10, 0), It.IsAny<CancellationToken>()))
            .ReturnsAsync(anchor);
        _appointmentSlotRepository
            .Setup(r => r.GetByVeterinarianDateStartAsync(
                VeterinarianId, FutureDate, new TimeOnly(11, 0), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppointmentSlot?)null);
        _appointmentSlotRepository
            .Setup(r => r.GetByStatusInWindowAsync(
                VeterinarianId, FutureDate, new TimeOnly(10, 0), new TimeOnly(11, 0),
                AppointmentSlotStatus.Reserved, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AppointmentSlot> { anchor });
        SetupTransaction();

        var service = CreateService();
        await service.UpdateAppointmentAsync(appointment.Id, new UpdateAppointmentRequest
        {
            ScheduledStart = FutureDate.ToDateTime(new TimeOnly(10, 0)),
            ScheduledEnd = FutureDate.ToDateTime(new TimeOnly(12, 0))
        });

        // Anchor kept + carries the full window end.
        Assert.Equal(AppointmentSlotStatus.Reserved, anchor.Status);
        Assert.Equal(new TimeOnly(12, 0), anchor.EndTime);
        // A Reserved slot row exists for the second hour.
        _appointmentSlotRepository.Verify(s => s.AddAsync(
            It.Is<AppointmentSlot>(x =>
                x.VeterinarianId == VeterinarianId &&
                x.Date == FutureDate &&
                x.StartTime == new TimeOnly(11, 0) &&
                x.EndTime == new TimeOnly(12, 0) &&
                x.Status == AppointmentSlotStatus.Reserved),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(new TimeOnly(12, 0), appointment.EndTime);
    }

    // 30. Shortening 2h -> 1h frees the second-hour reservation.
    [Fact]
    public async Task UpdateAppointmentAsync_ShortenToOneHour_ReleasesExtraSlot()
    {
        var anchor = Slot(VeterinarianId, new TimeOnly(10, 0), new TimeOnly(12, 0));
        anchor.Date = FutureDate;
        anchor.Status = AppointmentSlotStatus.Reserved;
        var second = Slot(VeterinarianId, new TimeOnly(11, 0), new TimeOnly(12, 0), Guid.NewGuid());
        second.Date = FutureDate;
        second.Status = AppointmentSlotStatus.Reserved;
        var appointment = BookedAppointment(
            VeterinarianId, anchor.Id, FutureDate, new TimeOnly(10, 0), new TimeOnly(12, 0));

        _appointmentRepository
            .Setup(r => r.GetByIdAsync(appointment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);
        _veterinarianRepository
            .Setup(r => r.GetByIdAsync(VeterinarianId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveVeterinarian());
        _appointmentRepository
            .Setup(r => r.GetActiveByVeterinarianAndDateAsync(
                VeterinarianId, FutureDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Appointment>());
        _appointmentSlotRepository
            .Setup(r => r.GetByVeterinarianDateStartAsync(
                VeterinarianId, FutureDate, new TimeOnly(10, 0), It.IsAny<CancellationToken>()))
            .ReturnsAsync(anchor);
        _appointmentSlotRepository
            .Setup(r => r.GetByStatusInWindowAsync(
                VeterinarianId, FutureDate, new TimeOnly(10, 0), new TimeOnly(12, 0),
                AppointmentSlotStatus.Reserved, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AppointmentSlot> { anchor, second });
        SetupTransaction();

        var service = CreateService();
        await service.UpdateAppointmentAsync(appointment.Id, new UpdateAppointmentRequest
        {
            ScheduledStart = FutureDate.ToDateTime(new TimeOnly(10, 0)),
            ScheduledEnd = FutureDate.ToDateTime(new TimeOnly(11, 0))
        });

        Assert.Equal(AppointmentSlotStatus.Reserved, anchor.Status);
        Assert.Equal(new TimeOnly(11, 0), anchor.EndTime);
        Assert.Equal(AppointmentSlotStatus.Available, second.Status);
        Assert.Equal(new TimeOnly(12, 0), second.EndTime); // restored to +1h
    }

    // 31. A window ending after closing time (18:00) is rejected up front.
    [Fact]
    public async Task UpdateAppointmentAsync_EndPastClosing_ThrowsConflict()
    {
        var appointment = BookedAppointment(
            VeterinarianId, Guid.NewGuid(), Date, new TimeOnly(10, 0), new TimeOnly(11, 0));
        _appointmentRepository
            .Setup(r => r.GetByIdAsync(appointment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);

        var service = CreateService();
        await Assert.ThrowsAsync<SchedulingConflictException>(
            () => service.UpdateAppointmentAsync(appointment.Id, new UpdateAppointmentRequest
            {
                ScheduledStart = FutureDate.ToDateTime(new TimeOnly(16, 0)),
                ScheduledEnd = FutureDate.ToDateTime(new TimeOnly(19, 0))
            }));
        _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // 32. A multi-hour move fails when a continuation hour is Reserved by
    //     another booking — no partial reservation.
    [Fact]
    public async Task UpdateAppointmentAsync_SecondHourTaken_ThrowsConflict()
    {
        var oldSlot = Slot(VeterinarianId, new TimeOnly(10, 0), new TimeOnly(11, 0));
        oldSlot.Status = AppointmentSlotStatus.Reserved;
        var appointment = BookedAppointment(
            VeterinarianId, oldSlot.Id, Date, new TimeOnly(10, 0), new TimeOnly(11, 0));
        var taken = new AppointmentSlot
        {
            Id = Guid.NewGuid(),
            VeterinarianId = VeterinarianId,
            Date = FutureDate,
            StartTime = new TimeOnly(15, 0),
            EndTime = new TimeOnly(16, 0),
            Branch = "Colombo",
            Status = AppointmentSlotStatus.Reserved
        };

        _appointmentRepository
            .Setup(r => r.GetByIdAsync(appointment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);
        _veterinarianRepository
            .Setup(r => r.GetByIdAsync(VeterinarianId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveVeterinarian());
        _appointmentRepository
            .Setup(r => r.GetActiveByVeterinarianAndDateAsync(
                VeterinarianId, FutureDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Appointment>());
        // Anchor hour free; the 15:00 continuation hour is already Reserved.
        _appointmentSlotRepository
            .Setup(r => r.GetByVeterinarianDateStartAsync(
                VeterinarianId, FutureDate, new TimeOnly(14, 0), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppointmentSlot?)null);
        _appointmentSlotRepository
            .Setup(r => r.GetByVeterinarianDateStartAsync(
                VeterinarianId, FutureDate, new TimeOnly(15, 0), It.IsAny<CancellationToken>()))
            .ReturnsAsync(taken);
        _appointmentSlotRepository
            .Setup(r => r.GetByStatusInWindowAsync(
                VeterinarianId, Date, new TimeOnly(10, 0), new TimeOnly(11, 0),
                AppointmentSlotStatus.Reserved, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AppointmentSlot> { oldSlot });
        SetupTransaction();

        var service = CreateService();
        await Assert.ThrowsAsync<SchedulingConflictException>(
            () => service.UpdateAppointmentAsync(appointment.Id, new UpdateAppointmentRequest
            {
                ScheduledStart = FutureDate.ToDateTime(new TimeOnly(14, 0)),
                ScheduledEnd = FutureDate.ToDateTime(new TimeOnly(16, 0))
            }));

        // The old reservation was never released — nothing partially moved.
        Assert.Equal(AppointmentSlotStatus.Reserved, oldSlot.Status);
    }

    // ---------------------------------------------------------------
    // CancelAppointmentAsync — linked-consultation synchronization
    // ---------------------------------------------------------------

    private Appointment BookedLinkedAppointment(string consultationId)
    {
        var appointment = BookedAppointment(
            VeterinarianId, Guid.NewGuid(), Date, new TimeOnly(10, 0), new TimeOnly(11, 0));
        appointment.ConsultationRequestId = consultationId;
        return appointment;
    }

    // 33. Cancelling a consultation-confirmed appointment cancels the
    //     request that drove it — matching terminal states, history kept.
    [Fact]
    public async Task CancelAppointmentAsync_LinkedConsultation_CancelsRequest()
    {
        var slot = Slot(VeterinarianId, new TimeOnly(10, 0), new TimeOnly(11, 0));
        slot.Status = AppointmentSlotStatus.Reserved;
        var appointment = BookedLinkedAppointment("CON-C1");
        var consultation = new ConsultationRequest
        {
            Id = "CON-C1",
            PetId = PetId,
            OwnerId = "OWN-TEST001",
            SymptomsDescription = "X",
            Status = "AppointmentConfirmed"
        };

        _appointmentRepository
            .Setup(r => r.GetByIdAsync(appointment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);
        _consultationRequests
            .Setup(r => r.GetByIdAsync("CON-C1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(consultation);
        _appointmentSlotRepository
            .Setup(r => r.GetByStatusInWindowAsync(
                VeterinarianId, Date, new TimeOnly(10, 0), new TimeOnly(11, 0),
                AppointmentSlotStatus.Reserved, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AppointmentSlot> { slot });

        var service = CreateService();
        await service.CancelAppointmentAsync(appointment.Id);

        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        Assert.Equal(AppointmentSlotStatus.Available, slot.Status);
        Assert.Equal("Cancelled", consultation.Status);
        Assert.Contains(consultation.StatusHistories, h => h.Status == "Cancelled");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // 34. A consultation that already moved past confirmation (e.g.
    //     Completed by the vet side) is never regressed by a cancel.
    [Fact]
    public async Task CancelAppointmentAsync_CompletedConsultation_NotRegressed()
    {
        var appointment = BookedLinkedAppointment("CON-C2");
        var consultation = new ConsultationRequest
        {
            Id = "CON-C2",
            PetId = PetId,
            OwnerId = "OWN-TEST001",
            SymptomsDescription = "X",
            Status = "Completed"
        };

        _appointmentRepository
            .Setup(r => r.GetByIdAsync(appointment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);
        _consultationRequests
            .Setup(r => r.GetByIdAsync("CON-C2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(consultation);
        _appointmentSlotRepository
            .Setup(r => r.GetByStatusInWindowAsync(
                It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<TimeOnly>(),
                It.IsAny<TimeOnly>(), It.IsAny<AppointmentSlotStatus>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AppointmentSlot>());

        var service = CreateService();
        await service.CancelAppointmentAsync(appointment.Id);

        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        Assert.Equal("Completed", consultation.Status);
        Assert.Empty(consultation.StatusHistories);
    }

    // 35. Cancelling an appointment with no consultation link behaves
    //     exactly as before — slot freed, no consultation touched.
    [Fact]
    public async Task CancelAppointmentAsync_NoLink_LeavesConsultationsAlone()
    {
        var appointment = BookedAppointment(
            VeterinarianId, Guid.NewGuid(), Date, new TimeOnly(10, 0), new TimeOnly(11, 0));
        _appointmentRepository
            .Setup(r => r.GetByIdAsync(appointment.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointment);
        _appointmentSlotRepository
            .Setup(r => r.GetByStatusInWindowAsync(
                It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<TimeOnly>(),
                It.IsAny<TimeOnly>(), It.IsAny<AppointmentSlotStatus>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AppointmentSlot>());

        var service = CreateService();
        await service.CancelAppointmentAsync(appointment.Id);

        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        _consultationRequests.Verify(
            r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
