using FluentValidation;
using PetCare.Application.DTOs.Organizations;
using PetCare.Application.DTOs.Users;
using PetCare.Domain.Constants;

namespace PetCare.Application.Validators.Users;

public sealed class CreateStaffUserRequestValidator : AbstractValidator<CreateStaffUserRequestDto>
{
    public CreateStaffUserRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100).WithMessage("First name must not exceed 100 characters.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100).WithMessage("Last name must not exceed 100 characters.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Staff role is required.")
            .Must(r => r is Roles.Veterinarian or Roles.InventoryOfficer)
            .WithMessage($"Staff role must be either '{Roles.Veterinarian}' or '{Roles.InventoryOfficer}'.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.");
    }
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequestDto>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(8).WithMessage("New password must be at least 8 characters.")
            .Matches("[A-Z]").WithMessage("New password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("New password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("New password must contain at least one digit.")
            .Matches("[^a-zA-Z0-9]").WithMessage("New password must contain at least one special character.");

        RuleFor(x => x.ConfirmNewPassword)
            .NotEmpty().WithMessage("Please confirm your new password.")
            .Equal(x => x.NewPassword).WithMessage("New passwords do not match.");
    }
}

public sealed class RejectOrganizationValidator : AbstractValidator<RejectOrganizationDto>
{
    public RejectOrganizationValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("A rejection reason is required.")
            .MaximumLength(1000).WithMessage("Rejection reason must not exceed 1000 characters.");
    }
}
