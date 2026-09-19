using FluentValidation;
using PetCare.Application.DTOs.Auth;

namespace PetCare.Application.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid email address.");

        RuleFor(r => r.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}
