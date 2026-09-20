using FluentValidation;
using PetCare.Application.DTOs.Auth;

namespace PetCare.Application.Validators;

/// <summary>
/// Validates PetOwner self-registration requests. Enforces the same
/// password complexity policy used for organization registration.
/// </summary>
public class RegisterPetOwnerRequestValidator : AbstractValidator<RegisterPetOwnerRequest>
{
    public RegisterPetOwnerRequestValidator()
    {
        RuleFor(r => r.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100).WithMessage("First name must be 100 characters or fewer.");

        RuleFor(r => r.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100).WithMessage("Last name must be 100 characters or fewer.");

        RuleFor(r => r.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email address.")
            .MaximumLength(256).WithMessage("Email must be 256 characters or fewer.");

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
