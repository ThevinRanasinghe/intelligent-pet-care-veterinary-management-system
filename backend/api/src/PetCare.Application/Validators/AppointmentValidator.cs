using FluentValidation;
using PetCare.Application.DTOs.Scheduling;
using PetCare.Application.Interfaces;

namespace PetCare.Application.Validators;

/// <summary>
/// Structural/referential validation for CreateAppointmentRequest: format
/// checks (start &lt; end, same-day, required fields) plus existence checks
/// for veterinarian and appointment slot IDs. Deeper business rules (vet
/// active, slot belongs to vet, appointment fits inside slot, overlap
/// conflicts) are enforced by SchedulingService, which needs to load the
/// same entities anyway to execute the use case.
///
/// PetId existence cannot be verified here: Pet is owned by another
/// module/component and no repository for it exists in this component
/// (see docs/database/scheduling-billing-approval-domain-model.md).
/// Only structural validation (non-empty) is applied to PetId.
/// </summary>
public class CreateAppointmentRequestValidator : AbstractValidator<CreateAppointmentRequest>
{
    public CreateAppointmentRequestValidator(
        IVeterinarianRepository veterinarianRepository,
        IAppointmentSlotRepository appointmentSlotRepository)
    {
        RuleFor(r => r.PetId)
            .NotEmpty();

        RuleFor(r => r.VeterinarianId)
            .NotEmpty()
            .MustAsync(async (id, cancellationToken) =>
                await veterinarianRepository.GetByIdAsync(id, cancellationToken) is not null)
            .WithMessage("Veterinarian does not exist.");

        RuleFor(r => r.AppointmentSlotId)
            .NotEmpty()
            .MustAsync(async (id, cancellationToken) =>
                await appointmentSlotRepository.GetByIdAsync(id, cancellationToken) is not null)
            .WithMessage("Appointment slot does not exist.");

        RuleFor(r => r.ScheduledStart)
            .LessThan(r => r.ScheduledEnd)
            .WithMessage("ScheduledStart must be before ScheduledEnd.");

        RuleFor(r => r)
            .Must(r => r.ScheduledStart.Date == r.ScheduledEnd.Date)
            .WithMessage("An appointment cannot span multiple days.");
    }
}

/// <summary>
/// Structural validation for UpdateAppointmentRequest (start &lt; end, same-day).
/// </summary>
public class UpdateAppointmentRequestValidator : AbstractValidator<UpdateAppointmentRequest>
{
    public UpdateAppointmentRequestValidator()
    {
        RuleFor(r => r.ScheduledStart)
            .LessThan(r => r.ScheduledEnd)
            .WithMessage("ScheduledStart must be before ScheduledEnd.");

        RuleFor(r => r)
            .Must(r => r.ScheduledStart.Date == r.ScheduledEnd.Date)
            .WithMessage("An appointment cannot span multiple days.");
    }
}
