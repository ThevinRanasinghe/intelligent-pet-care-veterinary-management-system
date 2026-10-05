using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PetCare.Application.DTOs.Consultations;
using PetCare.Application.DTOs.Scheduling;
using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;

namespace PetCare.Application.Services;

/// <summary>
/// Consultation-request workflow that spans scheduling: manager assignment
/// of a veterinarian (slot + Confirmed appointment in one unit of work) and
/// veterinarian-requested follow-up consultations.
/// </summary>
public class ConsultationWorkflowService : IConsultationWorkflowService
{
    private readonly IConsultationRequestRepository _consultations;
    private readonly IVeterinarianRepository _veterinarians;
    private readonly IAppointmentRepository _appointments;
    private readonly IAppointmentSlotRepository _slots;
    private readonly IPetService _pets;
    private readonly IExaminationRepository _examinations;
    private readonly ICurrentVeterinarianResolver _veterinarianResolver;
    private readonly ITenantContext _tenant;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<AssignVeterinarianRequest> _assignValidator;
    private readonly IValidator<CreateFollowUpRequest> _followUpValidator;

    public ConsultationWorkflowService(
        IConsultationRequestRepository consultations,
        IVeterinarianRepository veterinarians,
        IAppointmentRepository appointments,
        IAppointmentSlotRepository slots,
        IPetService pets,
        IExaminationRepository examinations,
        ICurrentVeterinarianResolver veterinarianResolver,
        ITenantContext tenant,
        IUnitOfWork unitOfWork,
        IValidator<AssignVeterinarianRequest> assignValidator,
        IValidator<CreateFollowUpRequest> followUpValidator)
    {
        _consultations = consultations;
        _veterinarians = veterinarians;
        _appointments = appointments;
        _slots = slots;
        _pets = pets;
        _examinations = examinations;
        _veterinarianResolver = veterinarianResolver;
        _tenant = tenant;
        _unitOfWork = unitOfWork;
        _assignValidator = assignValidator;
        _followUpValidator = followUpValidator;
    }

    public async Task<AppointmentResponse> AssignConsultationAsync(
        string consultationId,
        AssignVeterinarianRequest request,
        CancellationToken cancellationToken = default)
    {
        await _assignValidator.ValidateAndThrowAsync(request, cancellationToken);

        var consultation = await _consultations.GetByIdAsync(consultationId, cancellationToken)
            ?? throw new NotFoundException($"Consultation request '{consultationId}' does not exist.");

        if (consultation.Status is not ("Submitted" or "Processing"))
        {
            throw new SchedulingConflictException(
                $"Only Submitted or Processing consultation requests can be assigned. Current status: '{consultation.Status}'.");
        }

        // Org-scoped lookup: a veterinarian outside the caller's organization
        // resolves to null and is reported as not found.
        var veterinarian = await _veterinarians.GetByIdAsync(request.VeterinarianId, cancellationToken)
            ?? throw new NotFoundException($"Veterinarian '{request.VeterinarianId}' does not exist.");

        if (!veterinarian.Active)
        {
            throw new SchedulingConflictException($"Veterinarian '{request.VeterinarianId}' is not active.");
        }

        // The veterinarian must belong to the organization the request was
        // filed with (null-org legacy requests rely on the org-scoped
        // repository lookup for caller-side scoping).
        if (consultation.OrganizationId is Guid requestOrganizationId
            && veterinarian.OrganizationId != requestOrganizationId)
        {
            throw new SchedulingConflictException(
                "The veterinarian does not belong to the consultation's organization.");
        }

        // Fixed one-hour slots: a single booking ends at start + 1h; an
        // AI-workflow multi-slot booking spans N consecutive hours
        // (already validated when the client supplied EndTime explicitly).
        var requestedSlotIds = request.SlotIds is { Count: > 0 } ? request.SlotIds : null;
        var slotCount = requestedSlotIds?.Count ?? 1;
        var endTime = request.EndTime
            ?? request.StartTime.AddMinutes(BookingRules.SlotDurationMinutes * slotCount);

        // Same overlap rule as SchedulingService.CheckConflictAsync.
        var existing = await _appointments.GetActiveByVeterinarianAndDateAsync(
            veterinarian.Id, request.Date, cancellationToken);
        if (existing.Any(a => a.Status != AppointmentStatus.Cancelled
            && request.StartTime < a.EndTime && endTime > a.StartTime))
        {
            throw new SchedulingConflictException(
                "The veterinarian already has a conflicting appointment for the requested time.");
        }

        // The multi-slot path claims the published slots inside an explicit
        // transaction: the conditional reservation and the booking commit
        // or roll back together.
        var transaction = requestedSlotIds is null
            ? null
            : await _unitOfWork.BeginTransactionAsync(cancellationToken);
        Appointment? appointment = null;
        await using (transaction)
        {
        AppointmentSlot slot;
        if (requestedSlotIds is not null)
        {
            slot = await ReserveRequestedSlotsAsync(
                veterinarian, request.Date, request.StartTime, endTime,
                requestedSlotIds, cancellationToken);
        }
        else
        {
        // Adopt a pre-posted Available slot for this vet/date/start instead of
        // inserting a colliding row; anything else occupying the slot is a
        // conflict. With no row, materialize the Reserved slot as before.
        var existingSlot = await _slots.GetByVeterinarianDateStartAsync(
            veterinarian.Id, request.Date, request.StartTime, cancellationToken);
        if (existingSlot is not null)
        {
            if (existingSlot.Status != AppointmentSlotStatus.Available)
            {
                throw new SchedulingConflictException(BookingRules.SlotUnavailableMessage);
            }
            existingSlot.Status = AppointmentSlotStatus.Reserved;
            existingSlot.EndTime = endTime;
            existingSlot.UpdatedAt = DateTime.UtcNow;
            slot = existingSlot;
        }
        else
        {
            slot = new AppointmentSlot
            {
                VeterinarianId = veterinarian.Id,
                Date = request.Date,
                StartTime = request.StartTime,
                EndTime = endTime,
                Branch = string.IsNullOrWhiteSpace(veterinarian.Branch) ? "Main" : veterinarian.Branch,
                Status = AppointmentSlotStatus.Reserved
            };
            await _slots.AddAsync(slot, cancellationToken);
        }
        }

        appointment = new Appointment
        {
            PetId = consultation.PetId,
            VeterinarianId = veterinarian.Id,
            AppointmentSlotId = slot.Id,
            Date = request.Date,
            StartTime = request.StartTime,
            EndTime = endTime,
            Status = AppointmentStatus.Confirmed,
            Notes = request.Notes,
            ConsultationRequestId = consultation.Id,
            Type = consultation.RequestType == "FollowUp" ? AppointmentType.FollowUp : AppointmentType.Initial
        };
        await _appointments.AddAsync(appointment, cancellationToken);

        var note = $"Assigned to {veterinarian.Name} on {request.Date:yyyy-MM-dd} {request.StartTime:HH:mm}.";
        consultation.Status = "AppointmentConfirmed";
        consultation.StatusNotes = note;
        consultation.UpdatedAt = DateTime.UtcNow;
        consultation.StatusHistories.Add(new ConsultationStatusHistory
        {
            ConsultationRequestId = consultation.Id,
            Status = "AppointmentConfirmed",
            Comments = note,
            ChangedAt = DateTime.UtcNow
        });

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueSlotViolation(ex))
        {
            // Concurrent assign raced on the unique
            // IX_AppointmentSlot (VeterinarianId, Date, StartTime) index.
            throw new SchedulingConflictException(
                BookingRules.SlotUnavailableMessage);
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        }

        // Reload so the response carries the denormalised display fields.
        var saved = await _appointments.GetByIdAsync(appointment!.Id, cancellationToken) ?? appointment;
        return AppointmentMapper.ToResponse(saved);
    }

    /// <summary>
    /// Validates and atomically reserves the consecutive published slots the
    /// AI workflow proposed. Every listed slot must exist in the caller's
    /// organization, belong to the veterinarian and date, still be
    /// Available, and cover exactly [startTime, endTime) in order. The
    /// conditional UPDATE re-checks Available at write time, so a slot
    /// claimed between proposal and approval aborts the whole booking.
    /// Returns the anchor (first) slot for the appointment reference.
    /// </summary>
    private async Task<AppointmentSlot> ReserveRequestedSlotsAsync(
        Veterinarian veterinarian,
        DateOnly date,
        TimeOnly startTime,
        TimeOnly endTime,
        IReadOnlyList<Guid> slotIds,
        CancellationToken cancellationToken)
    {
        var candidates = await _slots.GetManyByIdsAsync(slotIds, cancellationToken);
        if (candidates.Count != slotIds.Count)
        {
            // Missing or wrong-organization ids are indistinguishable here.
            throw new SchedulingConflictException(
                "One or more proposed appointment slots do not exist.");
        }

        var byId = candidates.ToDictionary(s => s.Id);
        var expectedStart = startTime;
        foreach (var id in slotIds)
        {
            var candidate = byId[id];
            if (candidate.VeterinarianId != veterinarian.Id || candidate.Date != date)
            {
                throw new SchedulingConflictException(
                    "A proposed slot does not match the veterinarian and date being booked.");
            }
            if (candidate.StartTime != expectedStart)
            {
                throw new SchedulingConflictException(
                    "The proposed slots are not consecutive from the requested start time.");
            }
            if (candidate.Status != AppointmentSlotStatus.Available)
            {
                throw new SchedulingConflictException(BookingRules.SlotUnavailableMessage);
            }
            expectedStart = expectedStart.AddMinutes(BookingRules.SlotDurationMinutes);
        }
        if (expectedStart != endTime)
        {
            throw new SchedulingConflictException(
                "The proposed slots do not cover the requested time window.");
        }

        // Atomic claim: rows no longer Available are not updated, so any
        // shortfall means a concurrent booking won the race.
        var reserved = await _slots.TryReserveAvailableAsync(slotIds, cancellationToken);
        if (reserved != slotIds.Count)
        {
            throw new SchedulingConflictException(BookingRules.SlotUnavailableMessage);
        }

        // The anchor slot carries the appointment's full window end so
        // downstream queries see a coherent Reserved block.
        var anchor = byId[slotIds[0]];
        anchor.EndTime = endTime;
        anchor.UpdatedAt = DateTime.UtcNow;
        return anchor;
    }

    public async Task<ConsultationRequestDto> CreateFollowUpAsync(
        CreateFollowUpRequest request,
        CancellationToken cancellationToken = default)
    {
        await _followUpValidator.ValidateAndThrowAsync(request, cancellationToken);

        var pet = await _pets.GetByIdAsync(request.PetId)
            ?? throw new NotFoundException($"Pet '{request.PetId}' does not exist.");

        // Archived pets cannot start new consultations — including
        // veterinarian-requested follow-ups.
        if (pet.IsArchived)
        {
            throw new SchedulingConflictException(
                $"Pet '{request.PetId}' is archived and cannot start a new consultation. Restore the pet first.");
        }

        // Veterinarian callers must resolve to their own profile; other
        // staff callers (Admin) may have no linked veterinarian.
        var veterinarian = _tenant.IsInRole(Roles.Veterinarian)
            ? await _veterinarianResolver.ResolveRequiredAsync(cancellationToken)
            : await _veterinarianResolver.TryResolveAsync(cancellationToken);

        var examination = await _examinations.GetByIdAsync(request.ExaminationId, cancellationToken)
            ?? throw new NotFoundException($"Examination '{request.ExaminationId}' does not exist.");

        // Fall back to the examining veterinarian when the caller has no
        // own profile (e.g. an admin filing the follow-up).
        veterinarian ??= await _veterinarians.GetByIdAsync(examination.VeterinarianId, cancellationToken);

        // The follow-up belongs to the clinic that treated the pet: the
        // source appointment's consultation organization wins; otherwise the
        // veterinarian's own organization applies.
        var organizationId =
            examination.Appointment?.ConsultationRequest?.OrganizationId
            ?? veterinarian?.OrganizationId;

        var requesterNote = veterinarian is null
            ? "Follow-up requested by clinic staff."
            : $"Follow-up requested by {veterinarian.Name}.";

        var consultation = new ConsultationRequest
        {
            Id = $"CON-{Guid.NewGuid():N}".Substring(0, 12).ToUpper(),
            PetId = pet.Id,
            OwnerId = pet.OwnerId,
            SymptomsDescription = string.IsNullOrWhiteSpace(request.Notes)
                ? request.Reason.Trim()
                : $"{request.Reason.Trim()}\n{request.Notes.Trim()}",
            OrganizationId = organizationId,
            // The preferred date column carries the booked slot start
            // (date + hour-aligned time), stored as UTC-kind.
            PreferredDate = DateTime.SpecifyKind(
                request.PreferredDate.Date + request.PreferredTime.ToTimeSpan(),
                DateTimeKind.Utc),
            BudgetLimit = 0,
            Status = "Submitted",
            RequestType = "FollowUp",
            RequestedByVeterinarianId = veterinarian?.Id,
            StatusNotes = requesterNote,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        consultation.StatusHistories.Add(new ConsultationStatusHistory
        {
            ConsultationRequestId = consultation.Id,
            Status = "Submitted",
            Comments = requesterNote,
            ChangedAt = DateTime.UtcNow
        });

        await _consultations.AddAsync(consultation, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ConsultationRequestDto
        {
            Id = consultation.Id,
            PetId = consultation.PetId,
            OwnerId = consultation.OwnerId,
            PetName = pet.Name,
            Symptoms = consultation.SymptomsDescription,
            Urgency = "Medium",
            PreferredDate = consultation.PreferredDate,
            PreferredTime = consultation.PreferredDate.TimeOfDay,
            OrganizationId = consultation.OrganizationId,
            Budget = consultation.BudgetLimit,
            AdditionalNotes = consultation.StatusNotes,
            Status = consultation.Status,
            RequestType = consultation.RequestType,
            RequestedByVeterinarianId = consultation.RequestedByVeterinarianId,
            RequestedByVeterinarianName = veterinarian?.Name,
            CreatedAt = consultation.CreatedAt,
            UpdatedAt = consultation.UpdatedAt
        };
    }

    /// <summary>
    /// Detects a Postgres unique-constraint violation (SqlState 23505) on the
    /// unique (VeterinarianId, Date, StartTime) slot index without taking a
    /// direct Npgsql dependency — the inner PostgresException is identified
    /// by type name and read via reflection.
    /// </summary>
    private static bool IsUniqueSlotViolation(DbUpdateException exception)
    {
        for (var inner = exception.InnerException;
             inner is not null;
             inner = inner.InnerException)
        {
            if (inner.GetType().Name == "PostgresException"
                && inner.GetType().GetProperty("SqlState")?.GetValue(inner) as string == "23505")
            {
                return true;
            }
        }

        return false;
    }
}
