using FluentValidation;
using FluentValidation.Results;
using Moq;
using PetCare.Application.DTOs.Scheduling;
using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;
using PetCare.Application.Services;
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
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IValidator<CreateAppointmentRequest>> _createValidator = new();
    private readonly Mock<IValidator<UpdateAppointmentRequest>> _updateValidator = new();

    private static readonly Guid VeterinarianId = Guid.NewGuid();
    private static readonly Guid OtherVeterinarianId = Guid.NewGuid();
    private static readonly Guid SlotId = Guid.NewGuid();
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
    }

    private SchedulingService CreateService() => new(
        _appointmentRepository.Object,
        _appointmentSlotRepository.Object,
        _veterinarianRepository.Object,
        _unitOfWork.Object,
        _createValidator.Object,
        _updateValidator.Object);

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
        PetId = Guid.NewGuid(),
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
        PetId = Guid.NewGuid(),
        VeterinarianId = veterinarianId,
        AppointmentSlotId = appointmentSlotId,
        ScheduledStart = Date.ToDateTime(start),
        ScheduledEnd = Date.ToDateTime(end)
    };

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

        _veterinarianRepository.Setup(r => r.GetByIdAsync(VeterinarianId, It.IsAny<CancellationToken>())).ReturnsAsync(vet);
        _appointmentSlotRepository.Setup(r => r.GetByIdAsync(slot.Id, It.IsAny<CancellationToken>())).ReturnsAsync(slot);
        SetUpNoExistingAppointments(VeterinarianId);

        var service = CreateService();

        var result = await service.CreateAppointmentAsync(request);

        Assert.Equal(AppointmentStatus.Reserved.ToString(), result.Status);
        _appointmentRepository.Verify(r => r.AddAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
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

        _veterinarianRepository.Setup(r => r.GetByIdAsync(VeterinarianId, It.IsAny<CancellationToken>())).ReturnsAsync(vet);
        _appointmentSlotRepository.Setup(r => r.GetByIdAsync(slot.Id, It.IsAny<CancellationToken>())).ReturnsAsync(slot);
        SetUpExistingAppointments(VeterinarianId, existing);

        var service = CreateService();

        var result = await service.CreateAppointmentAsync(request);

        Assert.Equal(AppointmentStatus.Reserved.ToString(), result.Status);
    }

    // 7. Different veterinarian at the same time -> allowed.
    [Fact]
    public async Task CreateAppointmentAsync_DifferentVeterinarianSameTime_Allowed()
    {
        var vet = ActiveVeterinarian(OtherVeterinarianId);
        var slot = Slot(OtherVeterinarianId, new TimeOnly(10, 0), new TimeOnly(11, 0));
        var request = Request(OtherVeterinarianId, slot.Id, new TimeOnly(10, 0), new TimeOnly(11, 0));

        // Existing appointment belongs to a different veterinarian (VeterinarianId),
        // so the "other" veterinarian's day has no active appointments.
        _veterinarianRepository.Setup(r => r.GetByIdAsync(OtherVeterinarianId, It.IsAny<CancellationToken>())).ReturnsAsync(vet);
        _appointmentSlotRepository.Setup(r => r.GetByIdAsync(slot.Id, It.IsAny<CancellationToken>())).ReturnsAsync(slot);
        SetUpNoExistingAppointments(OtherVeterinarianId);

        var service = CreateService();

        var result = await service.CreateAppointmentAsync(request);

        Assert.Equal(AppointmentStatus.Reserved.ToString(), result.Status);
    }

    // 8. Cancelled appointment does not cause a conflict.
    [Fact]
    public async Task CreateAppointmentAsync_OverlappingCancelledAppointment_Allowed()
    {
        var vet = ActiveVeterinarian();
        var slot = Slot(VeterinarianId, new TimeOnly(9, 0), new TimeOnly(12, 0));
        var request = Request(VeterinarianId, slot.Id, new TimeOnly(10, 0), new TimeOnly(11, 0));
        var cancelled = ExistingAppointment(VeterinarianId, new TimeOnly(10, 0), new TimeOnly(11, 0), AppointmentStatus.Cancelled);

        _veterinarianRepository.Setup(r => r.GetByIdAsync(VeterinarianId, It.IsAny<CancellationToken>())).ReturnsAsync(vet);
        _appointmentSlotRepository.Setup(r => r.GetByIdAsync(slot.Id, It.IsAny<CancellationToken>())).ReturnsAsync(slot);
        SetUpExistingAppointments(VeterinarianId, cancelled);

        var service = CreateService();

        var result = await service.CreateAppointmentAsync(request);

        Assert.Equal(AppointmentStatus.Reserved.ToString(), result.Status);
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

    // 10. Appointment outside the selected slot is rejected.
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
}
