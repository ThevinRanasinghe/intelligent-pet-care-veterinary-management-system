using FluentValidation;
using PetCare.Application.DTOs.Scheduling;
using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;

namespace PetCare.Application.Services;

/// <summary>
/// Scheduling business logic. This is the single source of truth for
/// appointment creation/update/cancellation rules and the veterinarian
/// overlap check, shared by every client (React, Flutter, ...) through
/// ASP.NET Core. See docs/database/scheduling-billing-approval-domain-model.md
/// for the underlying business rules.
/// </summary>
public class SchedulingService : ISchedulingService
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IAppointmentSlotRepository _appointmentSlotRepository;
    private readonly IVeterinarianRepository _veterinarianRepository;
    private readonly IPetService _petService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateAppointmentRequest> _createValidator;
    private readonly IValidator<UpdateAppointmentRequest> _updateValidator;
    private readonly ICurrentVeterinarianResolver _veterinarianResolver;
    private readonly IOwnerAccessService _ownerAccess;
    private readonly ITenantContext _tenant;
    private readonly IConsultationRequestRepository _consultationRequests;

    public SchedulingService(
        IAppointmentRepository appointmentRepository,
        IAppointmentSlotRepository appointmentSlotRepository,
        IVeterinarianRepository veterinarianRepository,
        IPetService petService,
        IUnitOfWork unitOfWork,
        IValidator<CreateAppointmentRequest> createValidator,
        IValidator<UpdateAppointmentRequest> updateValidator,
        ICurrentVeterinarianResolver veterinarianResolver,
        IOwnerAccessService ownerAccess,
        ITenantContext tenant,
        IConsultationRequestRepository consultationRequests)
    {
        _appointmentRepository = appointmentRepository;
        _appointmentSlotRepository = appointmentSlotRepository;
        _veterinarianRepository = veterinarianRepository;
        _petService = petService;
        _unitOfWork = unitOfWork;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _veterinarianResolver = veterinarianResolver;
        _ownerAccess = ownerAccess;
        _tenant = tenant;
        _consultationRequests = consultationRequests;
    }

    public async Task<IReadOnlyList<AppointmentResponse>> GetAppointmentsAsync(CancellationToken cancellationToken = default)
    {
        var appointments = await _appointmentRepository.GetAllAsync(cancellationToken);
        return appointments.Select(ToResponse).ToList();
    }

    public async Task<AppointmentResponse?> GetAppointmentByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(id, cancellationToken);
        return appointment is null ? null : ToResponse(appointment);
    }

    public async Task<IReadOnlyList<AppointmentResponse>> GetMyAppointmentsAsync(
        string? status,
        DateOnly? from,
        DateOnly? to,
        string? petId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Appointment> appointments;

        if (_tenant.IsInRole(Roles.Veterinarian))
        {
            // Veterinarians see their own schedule — resolved server-side
            // from the caller's user account, never from client input.
            var veterinarian = await _veterinarianResolver.ResolveRequiredAsync(cancellationToken);
            appointments = await _appointmentRepository.GetByVeterinarianAsync(veterinarian.Id, cancellationToken);
        }
        else if (_ownerAccess.IsPetOwner)
        {
            var ownerId = await _ownerAccess.GetOwnerIdAsync(cancellationToken);
            appointments = ownerId is null
                ? Array.Empty<Appointment>()
                : await _appointmentRepository.GetByOwnerAsync(ownerId, cancellationToken);
        }
        else
        {
            appointments = await _appointmentRepository.GetAllAsync(cancellationToken);
        }

        var filtered = appointments.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<AppointmentStatus>(status, ignoreCase: true, out var parsedStatus))
        {
            filtered = filtered.Where(a => a.Status == parsedStatus);
        }

        if (from is not null)
        {
            filtered = filtered.Where(a => a.Date >= from.Value);
        }

        if (to is not null)
        {
            filtered = filtered.Where(a => a.Date <= to.Value);
        }

        if (!string.IsNullOrWhiteSpace(petId))
        {
            filtered = filtered.Where(a => a.PetId == petId);
        }

        return filtered
            .OrderBy(a => a.Date)
            .ThenBy(a => a.StartTime)
            .Select(ToResponse)
            .ToList();
    }

    public async Task<AppointmentResponse> CreateAppointmentAsync(CreateAppointmentRequest request, CancellationToken cancellationToken = default)
    {
        // Structural/referential validation (start < end, same-day, vet/slot exist).
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        // 1. Check pet exists (FK to the canonical Pets table).
        var pet = await _petService.GetByIdAsync(request.PetId)
            ?? throw new NotFoundException($"Pet '{request.PetId}' does not exist.");

        // Archived pets keep their history but cannot enter new workflows.
        if (pet.IsArchived)
        {
            throw new SchedulingConflictException(
                $"Pet '{request.PetId}' is archived and cannot be booked for a new appointment. Restore the pet first.");
        }

        // 2. Check veterinarian exists.
        var veterinarian = await _veterinarianRepository.GetByIdAsync(request.VeterinarianId, cancellationToken)
            ?? throw new NotFoundException($"Veterinarian '{request.VeterinarianId}' does not exist.");

        // 3. Check veterinarian is active.
        if (!veterinarian.Active)
        {
            throw new SchedulingConflictException($"Veterinarian '{request.VeterinarianId}' is not active.");
        }

        // 4. Check appointment slot exists.
        var slot = await _appointmentSlotRepository.GetByIdAsync(request.AppointmentSlotId, cancellationToken)
            ?? throw new NotFoundException($"Appointment slot '{request.AppointmentSlotId}' does not exist.");

        // 5. Check slot belongs to veterinarian.
        if (slot.VeterinarianId != request.VeterinarianId)
        {
            throw new SchedulingConflictException("The appointment slot does not belong to the specified veterinarian.");
        }

        var requestedDate = DateOnly.FromDateTime(request.ScheduledStart);
        var requestedStart = TimeOnly.FromDateTime(request.ScheduledStart);
        var requestedEnd = TimeOnly.FromDateTime(request.ScheduledEnd);

        // 6. Check requested time fits inside slot.
        if (requestedDate != slot.Date || requestedStart < slot.StartTime || requestedEnd > slot.EndTime)
        {
            throw new SchedulingConflictException("The requested appointment time does not fit inside the selected slot.");
        }

        // 7. Check no conflicting appointment exists.
        var hasConflict = await CheckConflictAsync(
            new ConflictCheckRequest
            {
                VeterinarianId = request.VeterinarianId,
                ScheduledStart = request.ScheduledStart,
                ScheduledEnd = request.ScheduledEnd
            },
            cancellationToken);

        if (hasConflict)
        {
            throw new SchedulingConflictException("The veterinarian already has a conflicting appointment for the requested time.");
        }

        // 8. Consultation link — required. Every appointment in this system
        //    is driven by a consultation request (owner-filed Initial or
        //    vet-requested FollowUp); the validator rejects link-less
        //    requests up front. The link must bind to a real, eligible
        //    request — the same eligibility and organization rules the
        //    assign path enforces — and produces the same end state:
        //    Confirmed appointment, linked request, status transition +
        //    history. Orphaned, consultation-less appointments cannot be
        //    created through this endpoint.
        var consultationId = request.ConsultationRequestId?.Trim();
        if (string.IsNullOrEmpty(consultationId))
        {
            throw new ArgumentException(
                "ConsultationRequestId is required — appointments are created through the consultation workflow.");
        }

        var consultation = await _consultationRequests.GetByIdAsync(
            consultationId, cancellationToken)
            ?? throw new NotFoundException(
                $"Consultation request '{request.ConsultationRequestId}' does not exist.");

        if (consultation.PetId != pet.Id)
        {
            throw new ArgumentException(
                "The consultation request does not belong to the selected pet.");
        }

        if (consultation.Status is not ("Submitted" or "Processing"))
        {
            throw new SchedulingConflictException(
                $"Only Submitted or Processing consultation requests can be linked to a new appointment. Current status: '{consultation.Status}'.");
        }

        if (consultation.OrganizationId is Guid consultationOrg
            && veterinarian.OrganizationId != consultationOrg)
        {
            throw new SchedulingConflictException(
                "The veterinarian does not belong to the consultation's organization.");
        }

        // 9. Create appointment.
        var appointment = new Appointment
        {
            PetId = request.PetId,
            VeterinarianId = request.VeterinarianId,
            AppointmentSlotId = request.AppointmentSlotId,
            Date = requestedDate,
            StartTime = requestedStart,
            EndTime = requestedEnd,
            Status = AppointmentStatus.Confirmed,
            Notes = request.Notes,
            ConsultationRequestId = consultation.Id,
            Type = consultation.RequestType == "FollowUp"
                ? AppointmentType.FollowUp
                : AppointmentType.Initial
        };

        slot.Status = AppointmentSlotStatus.Reserved;

        var note = $"Assigned to {veterinarian.Name} on {requestedDate:yyyy-MM-dd} {requestedStart:HH:mm}.";
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

        await _appointmentRepository.AddAsync(appointment, cancellationToken);

        // 10. Save changes (appointment insert + slot status update +
        //     consultation transition in one transaction/unit of work).
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(appointment);
    }

    public async Task<AppointmentResponse> UpdateAppointmentAsync(Guid id, UpdateAppointmentRequest request, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var appointment = await _appointmentRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Appointment '{id}' does not exist.");

        // History is immutable: cancelled/completed appointments cannot be
        // rescheduled or reassigned — book a new appointment instead.
        if (appointment.Status is AppointmentStatus.Cancelled or AppointmentStatus.Completed)
        {
            throw new SchedulingConflictException(
                $"A {appointment.Status} appointment cannot be modified.");
        }

        var newDate = DateOnly.FromDateTime(request.ScheduledStart);
        var newStart = TimeOnly.FromDateTime(request.ScheduledStart);
        var newEnd = TimeOnly.FromDateTime(request.ScheduledEnd);

        var targetVeterinarianId = request.VeterinarianId ?? appointment.VeterinarianId;
        var veterinarianChanged = targetVeterinarianId != appointment.VeterinarianId;
        var windowChanged = newDate != appointment.Date
            || newStart != appointment.StartTime
            || newEnd != appointment.EndTime;
        var slotChangeRequested = request.AppointmentSlotId is Guid requestedSlot
            && requestedSlot != appointment.AppointmentSlotId;

        // Notes-only edits skip slot work entirely. Contract: a null Notes
        // (field omitted) leaves the value untouched; an explicit empty
        // string clears it; anything else replaces it.
        if (!veterinarianChanged && !windowChanged && !slotChangeRequested)
        {
            if (request.Notes is not null)
            {
                appointment.Notes = NormalizeOptionalNote(request.Notes);
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return ToResponse(appointment);
        }

        // Any move follows the same booking rules as a new booking:
        // hour-aligned starts inside 09:00–17:00 and the window must end by
        // closing time (multi-hour bookings span consecutive one-hour slots).
        if (!BookingRules.IsHourAligned(newStart)
            || !BookingRules.IsWithinOperatingHours(newStart)
            || !BookingRules.IsHourAligned(newEnd)
            || newEnd > BookingRules.ClosingTime)
        {
            throw new SchedulingConflictException(
                "Appointments start on the hour between 09:00 and 17:00 and must end by 18:00 (consecutive one-hour slots).");
        }

        if (!BookingRules.IsInFuture(newDate, newStart))
        {
            throw new SchedulingConflictException(
                "The appointment date/time cannot be in the past.");
        }

        var veterinarian = await _veterinarianRepository.GetByIdAsync(targetVeterinarianId, cancellationToken)
            ?? throw new NotFoundException($"Veterinarian '{targetVeterinarianId}' does not exist.");

        if (!veterinarian.Active)
        {
            throw new SchedulingConflictException($"Veterinarian '{targetVeterinarianId}' is not active.");
        }

        var hasConflict = await CheckConflictAsync(
            new ConflictCheckRequest
            {
                VeterinarianId = targetVeterinarianId,
                ScheduledStart = request.ScheduledStart,
                ScheduledEnd = request.ScheduledEnd,
                AppointmentId = appointment.Id
            },
            cancellationToken);

        if (hasConflict)
        {
            throw new SchedulingConflictException("The veterinarian already has a conflicting appointment for the requested time.");
        }

        // Release + reserve must commit together or not at all.
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        // Slots this appointment currently reserves — needed to tell "ours"
        // (reusable) from another booking's reservations when the new window
        // overlaps the old one under the same veterinarian.
        var oldReserved = await GetReservedWindowSlotsAsync(appointment, cancellationToken);
        var oldReservedIds = oldReserved.Select(s => s.Id).ToHashSet();

        // Resolve and reserve every one-hour slot in [newStart, newEnd) —
        // the same model the assign path uses: adopt a published Available
        // slot, keep one of the appointment's own reservations, or
        // materialize a Reserved row. The anchor (first hour) carries the
        // appointment reference and the full window end.
        AppointmentSlot? anchor = null;
        var keepSlotIds = new HashSet<Guid>();

        for (var t = newStart; t < newEnd; t = t.AddMinutes(BookingRules.SlotDurationMinutes))
        {
            var isAnchor = t == newStart;
            AppointmentSlot? hourSlot;

            if (isAnchor && slotChangeRequested)
            {
                hourSlot = await _appointmentSlotRepository.GetByIdAsync(
                    request.AppointmentSlotId!.Value, cancellationToken)
                    ?? throw new NotFoundException(
                        $"Appointment slot '{request.AppointmentSlotId}' does not exist.");

                if (hourSlot.VeterinarianId != targetVeterinarianId
                    || hourSlot.Date != newDate
                    || hourSlot.StartTime != newStart)
                {
                    throw new SchedulingConflictException(
                        "The selected slot does not match the veterinarian, date and start time.");
                }
            }
            else
            {
                hourSlot = await _appointmentSlotRepository.GetByVeterinarianDateStartAsync(
                    targetVeterinarianId, newDate, t, cancellationToken);
            }

            if (hourSlot is null)
            {
                hourSlot = new AppointmentSlot
                {
                    VeterinarianId = targetVeterinarianId,
                    Date = newDate,
                    StartTime = t,
                    EndTime = t.AddMinutes(BookingRules.SlotDurationMinutes),
                    Branch = string.IsNullOrWhiteSpace(veterinarian.Branch) ? "Main" : veterinarian.Branch,
                    Status = AppointmentSlotStatus.Reserved
                };
                await _appointmentSlotRepository.AddAsync(hourSlot, cancellationToken);
            }
            else if (oldReservedIds.Contains(hourSlot.Id))
            {
                keepSlotIds.Add(hourSlot.Id);
            }
            else
            {
                if (hourSlot.Status != AppointmentSlotStatus.Available)
                {
                    throw new SchedulingConflictException(BookingRules.SlotUnavailableMessage);
                }

                hourSlot.Status = AppointmentSlotStatus.Reserved;
                hourSlot.EndTime = t.AddMinutes(BookingRules.SlotDurationMinutes);
                hourSlot.UpdatedAt = DateTime.UtcNow;
            }

            if (isAnchor)
            {
                anchor = hourSlot;
                anchor.EndTime = newEnd;
                anchor.UpdatedAt = DateTime.UtcNow;
            }
        }

        var targetSlot = anchor!;

        // Free the previously occupied Reserved window — except slots the
        // appointment is keeping (same-slot resizes). Uses the OLD vet/date/
        // window captured above, so this must run before the appointment
        // fields are mutated.
        ReleaseReservedSlots(oldReserved, keepSlotIds);

        appointment.VeterinarianId = targetVeterinarianId;
        appointment.AppointmentSlotId = targetSlot.Id;
        appointment.Date = newDate;
        appointment.StartTime = newStart;
        appointment.EndTime = newEnd;
        if (request.Notes is not null)
        {
            appointment.Notes = NormalizeOptionalNote(request.Notes);
        }

        // Keep the linked consultation in step: the preferred date mirrors
        // the new slot start and the change lands in its status history —
        // the mechanism the app has for audit/notification.
        if (appointment.ConsultationRequestId is string consultationId)
        {
            var consultation = await _consultationRequests.GetByIdAsync(consultationId, cancellationToken);
            if (consultation is not null)
            {
                var note = veterinarianChanged
                    ? $"Veterinarian reassigned to {veterinarian.Name}; appointment moved to {newDate:yyyy-MM-dd} {newStart:HH:mm}."
                    : $"Appointment rescheduled to {newDate:yyyy-MM-dd} {newStart:HH:mm}.";
                consultation.PreferredDate = DateTime.SpecifyKind(
                    newDate.ToDateTime(newStart), DateTimeKind.Utc);
                consultation.UpdatedAt = DateTime.UtcNow;
                consultation.StatusHistories.Add(new ConsultationStatusHistory
                {
                    ConsultationRequestId = consultation.Id,
                    Status = consultation.Status,
                    Comments = note,
                    ChangedAt = DateTime.UtcNow
                });
            }
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex) when (IsUniqueSlotViolation(ex))
        {
            // Concurrent writer raced on the unique
            // IX_AppointmentSlot (VeterinarianId, Date, StartTime) index.
            throw new SchedulingConflictException(BookingRules.SlotUnavailableMessage);
        }

        await transaction.CommitAsync(cancellationToken);

        return ToResponse(appointment);
    }

    public async Task CancelAppointmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Appointment '{id}' does not exist.");

        appointment.Status = AppointmentStatus.Cancelled;

        // Cancelled appointments do not block slots: free every Reserved
        // slot the appointment occupied.
        ReleaseReservedSlots(
            await GetReservedWindowSlotsAsync(appointment, cancellationToken),
            keepSlotIds: null);

        // Keep the linked consultation in step: cancelling the booking
        // closes the request that drove it. A cancelled consultation is the
        // same terminal state the owner-side cancel path uses, and the
        // transition lands in its status history. Only an
        // AppointmentConfirmed request is regressed — consultations that
        // already moved on (e.g. Completed) are left alone.
        if (appointment.ConsultationRequestId is string consultationId)
        {
            var consultation = await _consultationRequests.GetByIdAsync(consultationId, cancellationToken);
            if (consultation is not null && consultation.Status == "AppointmentConfirmed")
            {
                consultation.Status = "Cancelled";
                consultation.StatusNotes = "Appointment cancelled.";
                consultation.UpdatedAt = DateTime.UtcNow;
                consultation.StatusHistories.Add(new ConsultationStatusHistory
                {
                    ConsultationRequestId = consultation.Id,
                    Status = "Cancelled",
                    Comments = "Appointment cancelled.",
                    ChangedAt = DateTime.UtcNow
                });
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The Reserved slots inside the appointment's booked window.</summary>
    private async Task<IReadOnlyList<AppointmentSlot>> GetReservedWindowSlotsAsync(
        Appointment appointment, CancellationToken cancellationToken)
    {
        return await _appointmentSlotRepository.GetByStatusInWindowAsync(
            appointment.VeterinarianId, appointment.Date, appointment.StartTime,
            appointment.EndTime, AppointmentSlotStatus.Reserved, cancellationToken)
            ?? Array.Empty<AppointmentSlot>();
    }

    /// <summary>
    /// Frees the given Reserved slots. Multi-slot bookings reserve a
    /// consecutive window; each restored slot gets its own one-hour end time
    /// back (the anchor carries the full window end while booked). Slots the
    /// appointment is keeping (same-slot resizes) are skipped.
    /// </summary>
    private static void ReleaseReservedSlots(
        IEnumerable<AppointmentSlot> windowSlots,
        IReadOnlyCollection<Guid>? keepSlotIds)
    {
        foreach (var slot in windowSlots)
        {
            if (keepSlotIds is not null && keepSlotIds.Contains(slot.Id))
            {
                continue;
            }
            slot.Status = AppointmentSlotStatus.Available;
            slot.EndTime = slot.StartTime.AddMinutes(BookingRules.SlotDurationMinutes);
            slot.UpdatedAt = DateTime.UtcNow;
        }
    }

    public async Task<IReadOnlyList<AppointmentSlotResponse>> GetAvailableSlotsAsync(
        Guid? veterinarianId,
        DateOnly? date,
        CancellationToken cancellationToken = default)
    {
        var slots = await _appointmentSlotRepository.GetAvailableAsync(veterinarianId, date, cancellationToken);
        return slots.Select(ToSlotResponse).ToList();
    }

    public async Task<bool> CheckConflictAsync(ConflictCheckRequest request, CancellationToken cancellationToken = default)
    {
        var date = DateOnly.FromDateTime(request.ScheduledStart);
        var newStart = TimeOnly.FromDateTime(request.ScheduledStart);
        var newEnd = TimeOnly.FromDateTime(request.ScheduledEnd);

        var existingAppointments = await _appointmentRepository.GetActiveByVeterinarianAndDateAsync(
            request.VeterinarianId,
            date,
            cancellationToken);

        // Cancelled appointments do not block a slot; excluded defensively here
        // even though the repository is also expected to filter them out.
        return existingAppointments
            .Where(a => a.Id != request.AppointmentId && a.Status != AppointmentStatus.Cancelled)
            .Any(a => newStart < a.EndTime && newEnd > a.StartTime);
    }

    /// <summary>
    /// Notes update contract: an explicit empty/whitespace value clears the
    /// note; a real value is stored trimmed. A null request field never
    /// reaches here — callers check it first — so an omitted field always
    /// preserves the existing note.
    /// </summary>
    private static string? NormalizeOptionalNote(string notes)
        => string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

    /// <summary>
    /// Detects a Postgres unique-constraint violation (SqlState 23505) — the
    /// unique (VeterinarianId, Date, StartTime) slot index — without taking a
    /// direct Npgsql dependency (same approach as ConsultationWorkflowService).
    /// </summary>
    private static bool IsUniqueSlotViolation(Microsoft.EntityFrameworkCore.DbUpdateException exception)
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

    private static AppointmentResponse ToResponse(Appointment appointment) =>
        AppointmentMapper.ToResponse(appointment);

    private static AppointmentSlotResponse ToSlotResponse(AppointmentSlot slot) => new()
    {
        Id = slot.Id,
        VeterinarianId = slot.VeterinarianId,
        Date = slot.Date,
        StartTime = slot.StartTime,
        EndTime = slot.EndTime,
        Branch = slot.Branch,
        Status = slot.Status.ToString()
    };
}
