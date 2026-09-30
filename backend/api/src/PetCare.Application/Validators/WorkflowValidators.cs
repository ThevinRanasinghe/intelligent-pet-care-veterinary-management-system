using FluentValidation;
using PetCare.Application.DTOs;
using PetCare.Application.DTOs.Consultations;
using PetCare.Domain.Constants;

namespace PetCare.Application.Validators;

/// <summary>
/// Structural validation for assigning a veterinarian to a consultation
/// request. Bookings use fixed one-hour slots: StartTime must be
/// hour-aligned inside operating hours (09:00–17:00) and, when an EndTime
/// is supplied, it must equal StartTime + 1 hour.
/// </summary>
public class AssignVeterinarianRequestValidator : AbstractValidator<AssignVeterinarianRequest>
{
    public AssignVeterinarianRequestValidator()
    {
        RuleFor(r => r.VeterinarianId)
            .NotEmpty().WithMessage("VeterinarianId is required.");

        RuleFor(r => r.StartTime)
            .Must(BookingRules.IsHourAligned)
            .WithMessage("StartTime must be on the hour (e.g. 10:00).")
            .Must(BookingRules.IsWithinOperatingHours)
            .WithMessage("StartTime must be between 09:00 and 17:00 (one-hour slots, closing at 18:00).");

        RuleFor(r => r.Date)
            .Must((r, date) => BookingRules.IsInFuture(date, r.StartTime))
            .WithMessage("The appointment date/time cannot be in the past.");

        RuleFor(r => r.EndTime)
            .Must((r, endTime) => endTime == r.StartTime.AddHours(1))
            .When(r => r.EndTime.HasValue)
            .WithMessage("EndTime must be exactly one hour after StartTime.");

        RuleFor(r => r.Notes)
            .MaximumLength(1000);
    }
}

/// <summary>Structural validation for veterinarian follow-up requests.</summary>
public class CreateFollowUpRequestValidator : AbstractValidator<CreateFollowUpRequest>
{
    public CreateFollowUpRequestValidator()
    {
        RuleFor(r => r.PetId)
            .NotEmpty().WithMessage("Pet ID is required.")
            .MaximumLength(30);

        RuleFor(r => r.ExaminationId)
            .NotEmpty().WithMessage("ExaminationId is required.");

        RuleFor(r => r.PreferredDate)
            .NotEmpty().WithMessage("Preferred date is required.");

        RuleFor(r => r.PreferredTime)
            .NotEmpty().WithMessage("A time slot is required.")
            .Must(BookingRules.IsHourAligned)
            .WithMessage("Time slots start on the hour (e.g. 10:00).")
            .Must(BookingRules.IsWithinOperatingHours)
            .WithMessage("Appointments start between 09:00 and 17:00 (one-hour slots, closing at 18:00).");

        RuleFor(r => r)
            .Must(r => BookingRules.IsInFuture(
                DateOnly.FromDateTime(r.PreferredDate), r.PreferredTime))
            .When(r => r.PreferredDate != default)
            .WithMessage("The follow-up date/time cannot be in the past.")
            .OverridePropertyName(nameof(CreateFollowUpRequest.PreferredDate));

        RuleFor(r => r.Reason)
            .NotEmpty().WithMessage("Reason is required.")
            .MaximumLength(4000);

        RuleFor(r => r.Notes)
            .MaximumLength(1000);
    }
}

/// <summary>The reason is mandatory when marking a medicine request unavailable.</summary>
public class MarkPrescriptionUnavailableValidator : AbstractValidator<MarkPrescriptionUnavailableRequest>
{
    public MarkPrescriptionUnavailableValidator()
    {
        RuleFor(r => r.Reason)
            .NotEmpty().WithMessage("Reason is required.")
            .MaximumLength(500);
    }
}
