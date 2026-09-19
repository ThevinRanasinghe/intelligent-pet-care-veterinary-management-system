using FluentValidation;
using PetCare.Application.DTOs.Scheduling;
using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateAppointmentRequest> _createValidator;
    private readonly IValidator<UpdateAppointmentRequest> _updateValidator;

    public SchedulingService(
        IAppointmentRepository appointmentRepository,
        IAppointmentSlotRepository appointmentSlotRepository,
        IVeterinarianRepository veterinarianRepository,
        IUnitOfWork unitOfWork,
        IValidator<CreateAppointmentRequest> createValidator,
        IValidator<UpdateAppointmentRequest> updateValidator)
    {
        _appointmentRepository = appointmentRepository;
        _appointmentSlotRepository = appointmentSlotRepository;
        _veterinarianRepository = veterinarianRepository;
        _unitOfWork = unitOfWork;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
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

    public async Task<AppointmentResponse> CreateAppointmentAsync(CreateAppointmentRequest request, CancellationToken cancellationToken = default)
    {
        // Structural/referential validation (start < end, same-day, vet/slot exist).
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        // 1. Check veterinarian exists.
        var veterinarian = await _veterinarianRepository.GetByIdAsync(request.VeterinarianId, cancellationToken)
            ?? throw new NotFoundException($"Veterinarian '{request.VeterinarianId}' does not exist.");

        // 2. Check veterinarian is active.
        if (!veterinarian.Active)
        {
            throw new SchedulingConflictException($"Veterinarian '{request.VeterinarianId}' is not active.");
        }

        // 3. Check appointment slot exists.
        var slot = await _appointmentSlotRepository.GetByIdAsync(request.AppointmentSlotId, cancellationToken)
            ?? throw new NotFoundException($"Appointment slot '{request.AppointmentSlotId}' does not exist.");

        // 4. Check slot belongs to veterinarian.
        if (slot.VeterinarianId != request.VeterinarianId)
        {
            throw new SchedulingConflictException("The appointment slot does not belong to the specified veterinarian.");
        }

        var requestedDate = DateOnly.FromDateTime(request.ScheduledStart);
        var requestedStart = TimeOnly.FromDateTime(request.ScheduledStart);
        var requestedEnd = TimeOnly.FromDateTime(request.ScheduledEnd);

        // 5. Check requested time fits inside slot.
        if (requestedDate != slot.Date || requestedStart < slot.StartTime || requestedEnd > slot.EndTime)
        {
            throw new SchedulingConflictException("The requested appointment time does not fit inside the selected slot.");
        }

        // 6. Check no conflicting appointment exists.
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

        // 7. Create appointment.
        var appointment = new Appointment
        {
            PetId = request.PetId,
            VeterinarianId = request.VeterinarianId,
            AppointmentSlotId = request.AppointmentSlotId,
            Date = requestedDate,
            StartTime = requestedStart,
            EndTime = requestedEnd,
            Status = AppointmentStatus.Reserved,
            Notes = request.Notes
        };

        slot.Status = AppointmentSlotStatus.Reserved;

        await _appointmentRepository.AddAsync(appointment, cancellationToken);

        // 8. Save changes (appointment insert + slot status update in one transaction/unit of work).
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(appointment);
    }

    public async Task<AppointmentResponse> UpdateAppointmentAsync(Guid id, UpdateAppointmentRequest request, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var appointment = await _appointmentRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Appointment '{id}' does not exist.");

        var newDate = DateOnly.FromDateTime(request.ScheduledStart);
        var newStart = TimeOnly.FromDateTime(request.ScheduledStart);
        var newEnd = TimeOnly.FromDateTime(request.ScheduledEnd);

        var hasConflict = await CheckConflictAsync(
            new ConflictCheckRequest
            {
                VeterinarianId = appointment.VeterinarianId,
                ScheduledStart = request.ScheduledStart,
                ScheduledEnd = request.ScheduledEnd,
                AppointmentId = appointment.Id
            },
            cancellationToken);

        if (hasConflict)
        {
            throw new SchedulingConflictException("The veterinarian already has a conflicting appointment for the requested time.");
        }

        appointment.Date = newDate;
        appointment.StartTime = newStart;
        appointment.EndTime = newEnd;
        appointment.Notes = request.Notes;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(appointment);
    }

    public async Task CancelAppointmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var appointment = await _appointmentRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Appointment '{id}' does not exist.");

        appointment.Status = AppointmentStatus.Cancelled;

        // Cancelled appointments do not block a slot: free it for reuse.
        var slot = await _appointmentSlotRepository.GetByIdAsync(appointment.AppointmentSlotId, cancellationToken);
        if (slot is not null)
        {
            slot.Status = AppointmentSlotStatus.Available;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
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

    private static AppointmentResponse ToResponse(Appointment appointment) => new()
    {
        Id = appointment.Id,
        PetId = appointment.PetId,
        VeterinarianId = appointment.VeterinarianId,
        AppointmentSlotId = appointment.AppointmentSlotId,
        ScheduledStart = appointment.Date.ToDateTime(appointment.StartTime),
        ScheduledEnd = appointment.Date.ToDateTime(appointment.EndTime),
        Status = appointment.Status.ToString(),
        Notes = appointment.Notes,
        CreatedAt = appointment.CreatedAt,
        UpdatedAt = appointment.UpdatedAt
    };

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
