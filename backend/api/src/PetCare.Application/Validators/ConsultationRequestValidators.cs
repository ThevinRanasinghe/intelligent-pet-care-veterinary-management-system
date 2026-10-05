using FluentValidation;
using PetCare.Application.DTOs.Consultations;
using PetCare.Domain.Constants;

namespace PetCare.Application.Validators;

/// <summary>
/// Shared booking-rule checks for a (preferredDate, preferredTime) pair:
/// the slot start must be on the hour, inside 09:00–17:00 (so the one-hour
/// slot ends by 18:00), and the date/slot must not be in the past (today is
/// allowed while the slot start is still ahead of now).
/// </summary>
internal static class BookingSlotRules
{
    public static bool IsAligned(TimeSpan? time) =>
        time.HasValue && BookingRules.IsHourAligned(TimeOnly.FromTimeSpan(time.Value));

    public static bool IsWithinHours(TimeSpan? time) =>
        time.HasValue && BookingRules.IsWithinOperatingHours(TimeOnly.FromTimeSpan(time.Value));

    public static bool IsInFuture(DateTime? date, TimeSpan? time) =>
        date.HasValue && time.HasValue
        && BookingRules.IsInFuture(
            DateOnly.FromDateTime(date.Value),
            TimeOnly.FromTimeSpan(time.Value));
}

/// <summary>
/// Booking rules for owner-filed consultation requests: pet, organization,
/// date and a one-hour slot are all mandatory.
/// </summary>
public class CreateConsultationRequestValidator : AbstractValidator<CreateConsultationRequestDto>
{
    public CreateConsultationRequestValidator()
    {
        RuleFor(x => x.PetId)
            .NotEmpty().WithMessage("Pet ID is required.");

        RuleFor(x => x.Symptoms)
            .NotEmpty().WithMessage("Symptoms are required.");

        RuleFor(x => x.OrganizationId)
            .NotEmpty().WithMessage("An organization (clinic) must be selected.");

        RuleFor(x => x.PreferredDate)
            .NotNull().WithMessage("Preferred date is required.");

        // Separate RuleFors: a .When() condition would otherwise cascade to
        // the preceding NotNull in the same chain.
        RuleFor(x => x.PreferredTime)
            .NotNull().WithMessage("A time slot is required.");

        RuleFor(x => x.PreferredTime)
            .Must(BookingSlotRules.IsAligned)
                .When(x => x.PreferredTime.HasValue)
                .WithMessage("Time slots start on the hour (e.g. 10:00).")
            .Must(BookingSlotRules.IsWithinHours)
                .When(x => x.PreferredTime.HasValue)
                .WithMessage("Appointments start between 09:00 and 17:00 (one-hour slots, closing at 18:00).");

        RuleFor(x => x.PreferredDate)
            .Must((dto, date) => BookingSlotRules.IsInFuture(date, dto.PreferredTime))
            .When(x => x.PreferredDate.HasValue && x.PreferredTime.HasValue)
            .WithMessage("The selected date/time slot cannot be in the past.");

        RuleFor(x => x.Budget)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Budget.HasValue)
            .WithMessage("Budget cannot be negative.");
    }
}

/// <summary>
/// Same date/time rules for updates. Fields stay optional (partial update),
/// but date and time must always be supplied together and, when supplied,
/// satisfy the booking rules.
/// </summary>
public class UpdateConsultationRequestValidator : AbstractValidator<UpdateConsultationRequestDto>
{
    public UpdateConsultationRequestValidator()
    {
        RuleFor(x => x.Symptoms)
            .NotEmpty().WithMessage("Symptoms are required.");

        RuleFor(x => x.OrganizationId)
            .NotEmpty()
            .When(x => x.OrganizationId.HasValue)
            .WithMessage("OrganizationId cannot be empty.");

        RuleFor(x => x.PreferredTime)
            .NotNull()
            .When(x => x.PreferredDate.HasValue)
            .WithMessage("A time slot is required when changing the preferred date.");

        RuleFor(x => x.PreferredDate)
            .NotNull()
            .When(x => x.PreferredTime.HasValue)
            .WithMessage("Preferred date is required when changing the time slot.");

        RuleFor(x => x.PreferredTime)
            .Must(BookingSlotRules.IsAligned)
                .When(x => x.PreferredTime.HasValue)
                .WithMessage("Time slots start on the hour (e.g. 10:00).")
            .Must(BookingSlotRules.IsWithinHours)
                .When(x => x.PreferredTime.HasValue)
                .WithMessage("Appointments start between 09:00 and 17:00 (one-hour slots, closing at 18:00).");

        RuleFor(x => x.PreferredDate)
            .Must((dto, date) => BookingSlotRules.IsInFuture(date, dto.PreferredTime))
            .When(x => x.PreferredDate.HasValue && x.PreferredTime.HasValue)
            .WithMessage("The selected date/time slot cannot be in the past.");

        RuleFor(x => x.Budget)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Budget.HasValue)
            .WithMessage("Budget cannot be negative.");
    }
}
