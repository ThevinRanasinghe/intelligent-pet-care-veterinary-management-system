using FluentValidation;
using PetCare.Application.DTOs.Auth;

namespace PetCare.Application.Validators;

/// <summary>
/// Validates Veterinary Organization self-registration requests. Covers
/// both the organization details (step 1) and the primary ClinicManager
/// account (step 2) of the registration wizard.
/// </summary>
public class RegisterOrganizationRequestValidator : AbstractValidator<RegisterOrganizationRequest>
{
    public RegisterOrganizationRequestValidator()
    {
        // ── Organization Details ──
        RuleFor(r => r.OrganizationName)
            .NotEmpty().WithMessage("Organization name is required.")
            .MaximumLength(200).WithMessage("Organization name must be 200 characters or fewer.");

        RuleFor(r => r.OrganizationEmail)
            .NotEmpty().WithMessage("Organization email is required.")
            .EmailAddress().WithMessage("Organization email must be a valid email address.")
            .MaximumLength(256).WithMessage("Organization email must be 256 characters or fewer.");

        RuleFor(r => r.OrganizationPhone)
            .NotEmpty().WithMessage("Phone number is required.")
            .MaximumLength(50).WithMessage("Phone number must be 50 characters or fewer.");

        RuleFor(r => r.Address)
            .NotEmpty().WithMessage("Address is required.")
            .MaximumLength(300).WithMessage("Address must be 300 characters or fewer.");

        RuleFor(r => r.City)
            .NotEmpty().WithMessage("City is required.")
            .MaximumLength(100).WithMessage("City must be 100 characters or fewer.");

        RuleFor(r => r.Country)
            .NotEmpty().WithMessage("Country is required.")
            .MaximumLength(100).WithMessage("Country must be 100 characters or fewer.");

        RuleFor(r => r.RegistrationNumber)
            .MaximumLength(100).WithMessage("Registration number must be 100 characters or fewer.");

        // ── Primary Clinic Manager Account ──
        RuleFor(r => r.ManagerFirstName)
            .NotEmpty().WithMessage("Manager first name is required.")
            .MaximumLength(100).WithMessage("Manager first name must be 100 characters or fewer.");

        RuleFor(r => r.ManagerLastName)
            .NotEmpty().WithMessage("Manager last name is required.")
            .MaximumLength(100).WithMessage("Manager last name must be 100 characters or fewer.");

        RuleFor(r => r.ManagerEmail)
            .NotEmpty().WithMessage("Manager email is required.")
            .EmailAddress().WithMessage("Manager email must be a valid email address.")
            .MaximumLength(256).WithMessage("Manager email must be 256 characters or fewer.");

        RuleFor(r => r.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Must(p => p.Any(char.IsUpper)).WithMessage("Password must contain at least one uppercase letter.")
            .Must(p => p.Any(char.IsLower)).WithMessage("Password must contain at least one lowercase letter.")
            .Must(p => p.Any(char.IsDigit)).WithMessage("Password must contain at least one digit.")
            .Must(p => p.Any(c => !char.IsLetterOrDigit(c))).WithMessage("Password must contain at least one special character.");

        RuleFor(r => r.ConfirmPassword)
            .NotEmpty().WithMessage("Password confirmation is required.")
            .Equal(r => r.Password).WithMessage("Passwords do not match.");
    }
}
