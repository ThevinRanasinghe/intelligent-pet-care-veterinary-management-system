using FluentValidation;
using PetCare.Application.DTOs.Consultations;
using PetCare.Domain.Constants;

namespace PetCare.Application.Validators;

/// <summary>
/// Structural validation for the walk-in intake: exactly one way to resolve
/// the owner and the pet, the consultation fields the online booking
/// collects, and the same hour-aligned slot rules as AssignVeterinarianRequest.
/// </summary>
public class CreateWalkInBookingRequestValidator : AbstractValidator<CreateWalkInBookingRequest>
{
    public CreateWalkInBookingRequestValidator()
    {
        RuleFor(r => r.OwnerId)
            .NotEmpty()
            .When(r => r.NewOwner is null)
            .WithMessage("Select an existing owner or register a new one.");

        RuleFor(r => r)
            .Must(r => string.IsNullOrWhiteSpace(r.OwnerId) || r.NewOwner is null)
            .WithMessage("OwnerId and NewOwner cannot both be supplied.");

        RuleFor(r => r.NewOwner!.FullName)
            .NotEmpty().WithMessage("Owner full name is required.")
            .When(r => r.NewOwner is not null);

        RuleFor(r => r.NewOwner!.Email)
            .NotEmpty().WithMessage("Owner email is required.")
            .EmailAddress().WithMessage("Owner email is invalid.")
            .When(r => r.NewOwner is not null);

        RuleFor(r => r.PetId)
            .NotEmpty()
            .When(r => r.NewPet is null)
            .WithMessage("Select an existing pet or register a new one.");

        RuleFor(r => r)
            .Must(r => string.IsNullOrWhiteSpace(r.PetId) || r.NewPet is null)
            .WithMessage("PetId and NewPet cannot both be supplied.");

        RuleFor(r => r.NewPet!.Name)
            .NotEmpty().WithMessage("Pet name is required.")
            .When(r => r.NewPet is not null);

        RuleFor(r => r.NewPet!.Species)
            .NotEmpty().WithMessage("Pet species is required.")
            .When(r => r.NewPet is not null);

        // Pets.Breed is a NOT NULL column — reject blank breeds up front so
        // the registration cannot fail at insert time.
        RuleFor(r => r.NewPet!.Breed)
            .NotEmpty().WithMessage("Pet breed is required.")
            .When(r => r.NewPet is not null);

        RuleFor(r => r.Symptoms)
            .NotEmpty().WithMessage("Symptoms are required.")
            .MaximumLength(4000);

        RuleFor(r => r.Budget)
            .GreaterThanOrEqualTo(0)
            .When(r => r.Budget.HasValue)
            .WithMessage("Budget cannot be negative.");

        RuleFor(r => r.AdditionalNotes)
            .MaximumLength(1000);

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
    }
}
