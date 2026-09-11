using FluentValidation;
using PetCare.Application.DTOs.Auth;

namespace PetCare.Application.Validators.Auth;

public sealed class RegisterOrganizationRequestValidator : AbstractValidator<RegisterOrganizationRequestDto>
{
    public RegisterOrganizationRequestValidator()
    {
        // ── Organization Validation ──
        RuleFor(x => x.OrganizationName)
            .NotEmpty().WithMessage("Organization name is required.")
            .MaximumLength(200).WithMessage("Organization name must not exceed 200 characters.");

        RuleFor(x => x.RegistrationNumber)
            .MaximumLength(100).WithMessage("Registration number must not exceed 100 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.RegistrationNumber));

        RuleFor(x => x.OrganizationEmail)
            .NotEmpty().WithMessage("Organization email is required.")
            .EmailAddress().WithMessage("Please enter a valid organization email address.")
            .MaximumLength(256).WithMessage("Organization email must not exceed 256 characters.");

        RuleFor(x => x.OrganizationPhone)
            .NotEmpty().WithMessage("Organization phone number is required.")
            .MaximumLength(50).WithMessage("Phone number must not exceed 50 characters.");

        RuleFor(x => x.Address)
            .NotEmpty().WithMessage("Address is required.")
            .MaximumLength(300).WithMessage("Address must not exceed 300 characters.");

        RuleFor(x => x.City)
            .NotEmpty().WithMessage("City is required.")
            .MaximumLength(100).WithMessage("City must not exceed 100 characters.");

        RuleFor(x => x.Country)
            .NotEmpty().WithMessage("Country is required.")
            .MaximumLength(100).WithMessage("Country must not exceed 100 characters.");

        // ── Manager Account Validation ──
        RuleFor(x => x.ManagerFirstName)
            .NotEmpty().WithMessage("Manager first name is required.")
            .MaximumLength(100).WithMessage("Manager first name must not exceed 100 characters.");

        RuleFor(x => x.ManagerLastName)
            .NotEmpty().WithMessage("Manager last name is required.")
            .MaximumLength(100).WithMessage("Manager last name must not exceed 100 characters.");

        RuleFor(x => x.ManagerEmail)
            .NotEmpty().WithMessage("Manager email is required.")
            .EmailAddress().WithMessage("Please enter a valid manager email address.")
            .MaximumLength(256).WithMessage("Manager email must not exceed 256 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one digit.")
            .Matches(@"[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("Please confirm your password.")
            .Equal(x => x.Password).WithMessage("Passwords do not match.");
    }
}
