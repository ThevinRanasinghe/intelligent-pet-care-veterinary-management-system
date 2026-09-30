using FluentValidation;
using FluentValidation.Results;
using Moq;
using PetCare.Application.DTOs.Consultations;
using PetCare.Application.DTOs.Pets;
using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;
using PetCare.Application.Services;
using PetCare.Domain.Constants;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;
using Xunit;

namespace PetCare.Application.Tests.Services;

/// <summary>
/// Unit tests for ConsultationWorkflowService: manager assignment of a
/// veterinarian to a consultation request (slot + Confirmed appointment +
/// status transition in one unit of work) and veterinarian follow-up
/// requests. All dependencies are mocked — Application layer only.
/// </summary>
public class ConsultationWorkflowServiceTests
{
    private readonly Mock<IConsultationRequestRepository> _consultations = new();
    private readonly Mock<IVeterinarianRepository> _veterinarians = new();
    private readonly Mock<IAppointmentRepository> _appointments = new();
    private readonly Mock<IAppointmentSlotRepository> _slots = new();
    private readonly Mock<IPetService> _pets = new();
    private readonly Mock<IExaminationRepository> _examinations = new();
    private readonly Mock<ICurrentVeterinarianResolver> _veterinarianResolver = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IValidator<AssignVeterinarianRequest>> _assignValidator = new();
    private readonly Mock<IValidator<CreateFollowUpRequest>> _followUpValidator = new();

    private static readonly Guid VeterinarianId = Guid.NewGuid();
    private static readonly DateOnly Date = new(2025, 6, 3);

    public ConsultationWorkflowServiceTests()
    {
        _assignValidator
            .Setup(v => v.ValidateAsync(It.IsAny<AssignVeterinarianRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _followUpValidator
            .Setup(v => v.ValidateAsync(It.IsAny<CreateFollowUpRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
    }

    private ConsultationWorkflowService CreateService() => new(
        _consultations.Object,
        _veterinarians.Object,
        _appointments.Object,
        _slots.Object,
        _pets.Object,
        _examinations.Object,
        _veterinarianResolver.Object,
        _tenant.Object,
        _unitOfWork.Object,
        _assignValidator.Object,
        _followUpValidator.Object);

    private static ConsultationRequest SubmittedConsultation(string status = "Submitted") => new()
    {
        Id = "CON-TEST0001",
        PetId = "PET-TEST0001",
        OwnerId = "OWN-TEST0001",
        SymptomsDescription = "Limping",
        PreferredDate = DateTime.UtcNow.AddDays(2),
        BudgetLimit = 5000m,
        Status = status
    };

    private static Veterinarian ActiveVeterinarian() => new()
    {
        Id = VeterinarianId,
        Name = "Dr. Test Vet",
        Specialisation = "General",
        Branch = "Colombo",
        Active = true
    };

    private static AssignVeterinarianRequest AssignRequest() => new()
    {
        VeterinarianId = VeterinarianId,
        Date = Date,
        StartTime = new TimeOnly(10, 0),
        EndTime = new TimeOnly(11, 0),
        Notes = "Urgent case"
    };

    // 1. Assigning a Submitted request creates a Reserved slot, a Confirmed
    //    appointment linked to the request, and transitions the request to
    //    AppointmentConfirmed — all in one save.
    [Fact]
    public async Task AssignConsultationAsync_SubmittedRequest_CreatesSlotAppointmentAndConfirms()
    {
        var consultation = SubmittedConsultation();
        var request = AssignRequest();

        _consultations.Setup(r => r.GetByIdAsync(consultation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(consultation);
        _veterinarians.Setup(r => r.GetByIdAsync(VeterinarianId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveVeterinarian());
        _appointments
            .Setup(r => r.GetActiveByVeterinarianAndDateAsync(VeterinarianId, Date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Appointment>());
        // The service reloads the saved appointment for denormalised fields.
        _appointments.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => null);

        var service = CreateService();
        var result = await service.AssignConsultationAsync(consultation.Id, request);

        _slots.Verify(s => s.AddAsync(
            It.Is<AppointmentSlot>(x =>
                x.VeterinarianId == VeterinarianId &&
                x.Date == Date &&
                x.StartTime == request.StartTime &&
                x.EndTime == request.EndTime &&
                x.Status == AppointmentSlotStatus.Reserved),
            It.IsAny<CancellationToken>()), Times.Once);

        _appointments.Verify(a => a.AddAsync(
            It.Is<Appointment>(x =>
                x.PetId == consultation.PetId &&
                x.VeterinarianId == VeterinarianId &&
                x.Status == AppointmentStatus.Confirmed &&
                x.ConsultationRequestId == consultation.Id &&
                x.Type == AppointmentType.Initial &&
                x.Date == Date &&
                x.StartTime == request.StartTime &&
                x.EndTime == request.EndTime),
            It.IsAny<CancellationToken>()), Times.Once);

        Assert.Equal("AppointmentConfirmed", consultation.Status);
        Assert.Contains("Assigned to Dr. Test Vet", consultation.StatusNotes);
        Assert.Contains(consultation.StatusHistories, h => h.Status == "AppointmentConfirmed");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(AppointmentStatus.Confirmed.ToString(), result.Status);
    }

    // 2. A request that is not Submitted/Processing cannot be assigned.
    [Theory]
    [InlineData("Draft")]
    [InlineData("Approved")]
    [InlineData("AppointmentConfirmed")]
    [InlineData("Cancelled")]
    public async Task AssignConsultationAsync_NonSubmittedRequest_ThrowsConflict(string status)
    {
        var consultation = SubmittedConsultation(status);
        _consultations.Setup(r => r.GetByIdAsync(consultation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(consultation);

        var service = CreateService();

        await Assert.ThrowsAsync<SchedulingConflictException>(
            () => service.AssignConsultationAsync(consultation.Id, AssignRequest()));

        _appointments.Verify(a => a.AddAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // 3. An overlapping appointment on the veterinarian's day is rejected
    //    with the existing overlap rule (newStart < existingEnd &&
    //    newEnd > existingStart).
    [Fact]
    public async Task AssignConsultationAsync_OverlappingAppointment_ThrowsConflict()
    {
        var consultation = SubmittedConsultation();
        var existing = new Appointment
        {
            Id = Guid.NewGuid(),
            PetId = "PET-OTHER001",
            VeterinarianId = VeterinarianId,
            AppointmentSlotId = Guid.NewGuid(),
            Date = Date,
            StartTime = new TimeOnly(9, 30),
            EndTime = new TimeOnly(10, 30),
            Status = AppointmentStatus.Confirmed
        };

        _consultations.Setup(r => r.GetByIdAsync(consultation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(consultation);
        _veterinarians.Setup(r => r.GetByIdAsync(VeterinarianId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveVeterinarian());
        _appointments
            .Setup(r => r.GetActiveByVeterinarianAndDateAsync(VeterinarianId, Date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Appointment> { existing });

        var service = CreateService();

        await Assert.ThrowsAsync<SchedulingConflictException>(
            () => service.AssignConsultationAsync(consultation.Id, AssignRequest()));

        _slots.Verify(s => s.AddAsync(It.IsAny<AppointmentSlot>(), It.IsAny<CancellationToken>()), Times.Never);
        _appointments.Verify(a => a.AddAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // 4. A veterinarian outside the manager's organization resolves to null
    //    through the org-scoped repository and is reported as not found.
    [Fact]
    public async Task AssignConsultationAsync_VeterinarianOutsideOrg_ThrowsNotFound()
    {
        var consultation = SubmittedConsultation();
        _consultations.Setup(r => r.GetByIdAsync(consultation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(consultation);
        _veterinarians.Setup(r => r.GetByIdAsync(VeterinarianId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Veterinarian?)null);

        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.AssignConsultationAsync(consultation.Id, AssignRequest()));

        _appointments.Verify(a => a.AddAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // 5. A follow-up creates a Submitted ConsultationRequest with
    //    RequestType = FollowUp and the resolved veterinarian id.
    [Fact]
    public async Task CreateFollowUpAsync_CreatesSubmittedFollowUpWithVeterinarian()
    {
        var veterinarian = ActiveVeterinarian();
        var examinationId = Guid.NewGuid();
        var request = new CreateFollowUpRequest
        {
            PetId = "PET-TEST0001",
            ExaminationId = examinationId,
            PreferredDate = DateTime.UtcNow.AddDays(7),
            PreferredTime = new TimeOnly(10, 0),
            Reason = "Recheck after surgery",
            Notes = "Bring vaccination card"
        };

        _tenant.Setup(t => t.IsInRole(PetCare.Domain.Constants.Roles.Veterinarian)).Returns(true);
        _veterinarianResolver.Setup(r => r.ResolveRequiredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(veterinarian);
        _examinations.Setup(e => e.GetByIdAsync(examinationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Examination { Id = examinationId, VeterinarianId = VeterinarianId });
        _pets.Setup(s => s.GetByIdAsync(request.PetId))
            .ReturnsAsync(new PetDto
            {
                Id = request.PetId,
                OwnerId = "OWN-TEST0001",
                Name = "Shadow",
                Species = "Dog"
            });

        ConsultationRequest? added = null;
        _consultations
            .Setup(r => r.AddAsync(It.IsAny<ConsultationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ConsultationRequest, CancellationToken>((c, _) => added = c);

        var service = CreateService();
        var result = await service.CreateFollowUpAsync(request);

        Assert.NotNull(added);
        Assert.Equal("FollowUp", added!.RequestType);
        Assert.Equal("Submitted", added.Status);
        Assert.Equal(VeterinarianId, added.RequestedByVeterinarianId);
        Assert.Equal(request.PetId, added.PetId);
        Assert.Equal("OWN-TEST0001", added.OwnerId);
        Assert.Contains("Recheck after surgery", added.SymptomsDescription);
        Assert.Contains(added.StatusHistories, h => h.Status == "Submitted");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        Assert.Equal("FollowUp", result.RequestType);
        Assert.Equal("Submitted", result.Status);
        Assert.Equal(VeterinarianId, result.RequestedByVeterinarianId);
        Assert.Equal("Dr. Test Vet", result.RequestedByVeterinarianName);
    }

    // 7. The follow-up request inherits the organization from the source
    //    examination's appointment consultation chain.
    [Fact]
    public async Task CreateFollowUpAsync_UsesExaminationConsultationOrganization()
    {
        var veterinarian = ActiveVeterinarian();
        var clinicId = Guid.NewGuid();
        var examinationId = Guid.NewGuid();
        var request = new CreateFollowUpRequest
        {
            PetId = "PET-TEST0001",
            ExaminationId = examinationId,
            PreferredDate = DateTime.UtcNow.AddDays(7),
            PreferredTime = new TimeOnly(14, 0),
            Reason = "Recheck"
        };

        _tenant.Setup(t => t.IsInRole(PetCare.Domain.Constants.Roles.Veterinarian)).Returns(true);
        _veterinarianResolver.Setup(r => r.ResolveRequiredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(veterinarian);
        _examinations.Setup(e => e.GetByIdAsync(examinationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Examination
            {
                Id = examinationId,
                VeterinarianId = VeterinarianId,
                Appointment = new Appointment
                {
                    ConsultationRequest = new ConsultationRequest { OrganizationId = clinicId }
                }
            });
        _pets.Setup(s => s.GetByIdAsync(request.PetId))
            .ReturnsAsync(new PetDto { Id = request.PetId, OwnerId = "OWN-TEST0001", Name = "Shadow", Species = "Dog" });

        ConsultationRequest? added = null;
        _consultations
            .Setup(r => r.AddAsync(It.IsAny<ConsultationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ConsultationRequest, CancellationToken>((c, _) => added = c);

        var service = CreateService();
        var result = await service.CreateFollowUpAsync(request);

        Assert.NotNull(added);
        Assert.Equal(clinicId, added!.OrganizationId);
        // The hour-aligned slot start is stored inside PreferredDate.
        Assert.Equal(new TimeOnly(14, 0).ToTimeSpan(), added.PreferredDate.TimeOfDay);
        Assert.Equal(clinicId, result.OrganizationId);
        Assert.Equal(new TimeSpan(14, 0, 0), result.PreferredTime);
    }

    // 8. When the examination's appointment has no linked consultation
    //    organization, the follow-up falls back to the veterinarian's own
    //    organization.
    [Fact]
    public async Task CreateFollowUpAsync_FallsBackToVeterinarianOrganization()
    {
        var vetOrg = Guid.NewGuid();
        var veterinarian = ActiveVeterinarian();
        veterinarian.OrganizationId = vetOrg;
        var examinationId = Guid.NewGuid();
        var request = new CreateFollowUpRequest
        {
            PetId = "PET-TEST0001",
            ExaminationId = examinationId,
            PreferredDate = DateTime.UtcNow.AddDays(7),
            PreferredTime = new TimeOnly(9, 0),
            Reason = "Recheck"
        };

        _tenant.Setup(t => t.IsInRole(PetCare.Domain.Constants.Roles.Veterinarian)).Returns(true);
        _veterinarianResolver.Setup(r => r.ResolveRequiredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(veterinarian);
        _examinations.Setup(e => e.GetByIdAsync(examinationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Examination
            {
                Id = examinationId,
                VeterinarianId = VeterinarianId,
                Appointment = new Appointment() // no ConsultationRequest
            });
        _pets.Setup(s => s.GetByIdAsync(request.PetId))
            .ReturnsAsync(new PetDto { Id = request.PetId, OwnerId = "OWN-TEST0001", Name = "Shadow", Species = "Dog" });

        ConsultationRequest? added = null;
        _consultations
            .Setup(r => r.AddAsync(It.IsAny<ConsultationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ConsultationRequest, CancellationToken>((c, _) => added = c);

        var service = CreateService();
        await service.CreateFollowUpAsync(request);

        Assert.Equal(vetOrg, added!.OrganizationId);
    }

    // 9. A missing examination is a 404.
    [Fact]
    public async Task CreateFollowUpAsync_UnknownExamination_ThrowsNotFound()
    {
        var request = new CreateFollowUpRequest
        {
            PetId = "PET-TEST0001",
            ExaminationId = Guid.NewGuid(),
            PreferredDate = DateTime.UtcNow.AddDays(7),
            PreferredTime = new TimeOnly(9, 0),
            Reason = "Recheck"
        };

        _tenant.Setup(t => t.IsInRole(PetCare.Domain.Constants.Roles.Veterinarian)).Returns(true);
        _veterinarianResolver.Setup(r => r.ResolveRequiredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveVeterinarian());
        _pets.Setup(s => s.GetByIdAsync(request.PetId))
            .ReturnsAsync(new PetDto { Id = request.PetId, OwnerId = "OWN-TEST0001", Name = "Shadow", Species = "Dog" });
        _examinations.Setup(e => e.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Examination?)null);

        var service = CreateService();
        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateFollowUpAsync(request));
    }

    // 10. An omitted EndTime is computed as StartTime + 1 hour.
    [Fact]
    public async Task AssignConsultationAsync_OmittedEndTime_ComputesOneHourSlot()
    {
        var consultation = SubmittedConsultation();
        var request = AssignRequest();
        request.EndTime = null;

        _consultations.Setup(r => r.GetByIdAsync(consultation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(consultation);
        _veterinarians.Setup(r => r.GetByIdAsync(VeterinarianId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveVeterinarian());
        _appointments
            .Setup(r => r.GetActiveByVeterinarianAndDateAsync(VeterinarianId, Date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Appointment>());
        _appointments.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => null);

        var service = CreateService();
        await service.AssignConsultationAsync(consultation.Id, request);

        _appointments.Verify(a => a.AddAsync(
            It.Is<Appointment>(x => x.EndTime == request.StartTime.AddHours(1)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // 11. A veterinarian outside the consultation's organization is rejected.
    [Fact]
    public async Task AssignConsultationAsync_VeterinarianOutsideRequestOrg_ThrowsConflict()
    {
        var consultation = SubmittedConsultation();
        consultation.OrganizationId = Guid.NewGuid();
        var vet = ActiveVeterinarian();
        vet.OrganizationId = Guid.NewGuid(); // different clinic

        _consultations.Setup(r => r.GetByIdAsync(consultation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(consultation);
        _veterinarians.Setup(r => r.GetByIdAsync(VeterinarianId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vet);

        var service = CreateService();

        var ex = await Assert.ThrowsAsync<SchedulingConflictException>(
            () => service.AssignConsultationAsync(consultation.Id, AssignRequest()));
        Assert.Contains("does not belong", ex.Message);
        _appointments.Verify(a => a.AddAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // 12. A concurrent assign racing the unique (VeterinarianId, Date,
    //     StartTime) slot index surfaces as a friendly 409.
    [Fact]
    public async Task AssignConsultationAsync_UniqueSlotViolation_ThrowsConflict()
    {
        var consultation = SubmittedConsultation();
        _consultations.Setup(r => r.GetByIdAsync(consultation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(consultation);
        _veterinarians.Setup(r => r.GetByIdAsync(VeterinarianId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveVeterinarian());
        _appointments
            .Setup(r => r.GetActiveByVeterinarianAndDateAsync(VeterinarianId, Date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Appointment>());

        // Fabricated PostgresException (SqlState 23505) — detected by type
        // name + SqlState via reflection inside the service.
        _unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Microsoft.EntityFrameworkCore.DbUpdateException(
                "duplicate key", new PostgresException("23505")));

        var service = CreateService();

        var ex = await Assert.ThrowsAsync<SchedulingConflictException>(
            () => service.AssignConsultationAsync(consultation.Id, AssignRequest()));
        Assert.Equal(BookingRules.SlotUnavailableMessage, ex.Message);
    }

    /// <summary>Test double for Npgsql.PostgresException — the service
    /// detects it by type name and the SqlState property via reflection.</summary>
    private sealed class PostgresException : Exception
    {
        public PostgresException(string sqlState) => SqlState = sqlState;
        public string SqlState { get; }
    }

    // 6. An unlinked veterinarian user cannot file a follow-up.
    [Fact]
    public async Task CreateFollowUpAsync_UnlinkedVeterinarian_ThrowsForbidden()
    {
        var request = new CreateFollowUpRequest
        {
            PetId = "PET-TEST0001",
            PreferredDate = DateTime.UtcNow.AddDays(7),
            Reason = "Recheck"
        };

        _tenant.Setup(t => t.IsInRole(PetCare.Domain.Constants.Roles.Veterinarian)).Returns(true);
        _pets.Setup(s => s.GetByIdAsync(request.PetId))
            .ReturnsAsync(new PetDto { Id = request.PetId, OwnerId = "OWN-TEST0001", Name = "Shadow", Species = "Dog" });
        _veterinarianResolver.Setup(r => r.ResolveRequiredAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ForbiddenException("Your account is not linked to a veterinarian profile."));

        var service = CreateService();

        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateFollowUpAsync(request));
        _consultations.Verify(r => r.AddAsync(It.IsAny<ConsultationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
