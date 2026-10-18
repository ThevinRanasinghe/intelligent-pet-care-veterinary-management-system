using FluentValidation;
using PetCare.Application.DTOs.Consultations;
using PetCare.Application.DTOs.PetOwners;
using PetCare.Application.DTOs.Pets;
using PetCare.Application.DTOs.Scheduling;
using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;

namespace PetCare.Application.Services;

/// <summary>
/// Walk-in intake for clinic staff. Rather than creating a bare appointment
/// (which bypasses the consultation workflow), this composes the exact steps
/// an online booking takes — owner resolution, pet resolution, consultation
/// create → submit → veterinarian assignment — inside one transaction, so the
/// persisted records are identical: a ConsultationRequest marked
/// Source="WalkIn" linked to a Confirmed appointment via
/// ConsultationRequestId.
/// </summary>
public class WalkInBookingService : IWalkInBookingService
{
    private readonly IValidator<CreateWalkInBookingRequest> _validator;
    private readonly IPetOwnerService _owners;
    private readonly IPetOwnerRepository _ownerRepository;
    private readonly IPetService _pets;
    private readonly IConsultationRequestService _consultations;
    private readonly IConsultationRequestRepository _consultationEntities;
    private readonly IConsultationWorkflowService _workflow;
    private readonly ITenantContext _tenant;
    private readonly IUnitOfWork _unitOfWork;

    public WalkInBookingService(
        IValidator<CreateWalkInBookingRequest> validator,
        IPetOwnerService owners,
        IPetOwnerRepository ownerRepository,
        IPetService pets,
        IConsultationRequestService consultations,
        IConsultationRequestRepository consultationEntities,
        IConsultationWorkflowService workflow,
        ITenantContext tenant,
        IUnitOfWork unitOfWork)
    {
        _validator = validator;
        _owners = owners;
        _ownerRepository = ownerRepository;
        _pets = pets;
        _consultations = consultations;
        _consultationEntities = consultationEntities;
        _workflow = workflow;
        _tenant = tenant;
        _unitOfWork = unitOfWork;
    }

    public async Task<WalkInBookingResponse> CreateAsync(
        CreateWalkInBookingRequest request,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        // Organization-scoped staff book at their own clinic; the platform
        // admin (unscoped) must name one explicitly.
        var organizationId = request.OrganizationId
            ?? await _tenant.GetOrganizationIdAsync(cancellationToken)
            ?? throw new ArgumentException("An organization (clinic) is required.");

        await using var transaction =
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

        // ---------------------------------------------------------------
        // A. Owner — existing profile, or register/reuse by email.
        // ---------------------------------------------------------------
        PetOwnerDto owner;
        if (!string.IsNullOrWhiteSpace(request.OwnerId))
        {
            owner = await _owners.GetByIdAsync(request.OwnerId.Trim())
                ?? throw new NotFoundException(
                    $"Pet owner '{request.OwnerId}' does not exist.");
        }
        else
        {
            var email = request.NewOwner!.Email.Trim();
            var existing = await _ownerRepository.GetByEmailAsync(email, cancellationToken);
            owner = existing is not null
                // Email is the dedupe key — never auto-match on names.
                ? await _owners.GetByIdAsync(existing.Id)
                    ?? throw new NotFoundException(
                        $"Pet owner '{existing.Id}' does not exist.")
                : await _owners.CreateAsync(request.NewOwner!);
        }

        // ---------------------------------------------------------------
        // B. Pet — must belong to the resolved owner (enforced again by the
        //    consultation create + submit paths).
        // ---------------------------------------------------------------
        PetDto pet;
        if (!string.IsNullOrWhiteSpace(request.PetId))
        {
            pet = await _pets.GetByIdAsync(request.PetId.Trim())
                ?? throw new NotFoundException(
                    $"Pet '{request.PetId}' does not exist.");

            if (pet.OwnerId != owner.Id)
            {
                throw new ArgumentException(
                    "The selected pet does not belong to the selected owner.");
            }

            if (pet.IsArchived)
            {
                throw new SchedulingConflictException(
                    $"Pet '{request.PetId}' is archived and cannot be booked for a new consultation. Restore the pet first.");
            }
        }
        else
        {
            request.NewPet!.OwnerId = owner.Id;
            pet = await _pets.CreateAsync(request.NewPet!);
        }

        // ---------------------------------------------------------------
        // C. Consultation — the same draft record an online booking files
        //    (org capacity + pet/owner checks run inside CreateAsync).
        // ---------------------------------------------------------------
        var consultation = await _consultations.CreateAsync(new CreateConsultationRequestDto
        {
            PetId = pet.Id,
            OwnerId = owner.Id,
            Symptoms = request.Symptoms.Trim(),
            Urgency = request.Urgency,
            OrganizationId = organizationId,
            PreferredDate = request.Date.ToDateTime(request.StartTime),
            PreferredTime = request.StartTime.ToTimeSpan(),
            Budget = request.Budget,
            AdditionalNotes = request.AdditionalNotes
        });

        // Mark the intake channel on the tracked entity; the upcoming Submit
        // save persists it together with the status change.
        var entity = await _consultationEntities.GetByIdAsync(consultation.Id, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Consultation '{consultation.Id}' could not be reloaded after creation.");
        entity.Source = "WalkIn";

        // ---------------------------------------------------------------
        // D+E. Submit and assign through the same authoritative path used by
        //      online bookings: Submit re-verifies ownership and slot
        //      capacity; Assign reserves the slot, creates the Confirmed
        //      appointment linked via ConsultationRequestId and transitions
        //      the request to AppointmentConfirmed.
        // ---------------------------------------------------------------
        var submitted = await _consultations.SubmitAsync(consultation.Id)
            ?? throw new InvalidOperationException(
                $"Consultation '{consultation.Id}' could not be submitted.");

        // Appointment notes carry the walk-in marker plus the intake notes
        // (capped at the 1000-char AssignVeterinarianRequest.Notes limit).
        var appointmentNotes = string.IsNullOrWhiteSpace(request.AdditionalNotes)
            ? "Walk-in booking."
            : $"Walk-in booking. {request.AdditionalNotes.Trim()}";
        if (appointmentNotes.Length > 1000)
        {
            appointmentNotes = appointmentNotes[..1000];
        }

        var appointment = await _workflow.AssignConsultationAsync(
            consultation.Id,
            new AssignVeterinarianRequest
            {
                VeterinarianId = request.VeterinarianId,
                Date = request.Date,
                StartTime = request.StartTime,
                Notes = appointmentNotes
            },
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var saved = await _consultations.GetByIdAsync(consultation.Id) ?? submitted;

        return new WalkInBookingResponse
        {
            Consultation = saved,
            Appointment = appointment,
            Owner = owner,
            Pet = pet
        };
    }
}
