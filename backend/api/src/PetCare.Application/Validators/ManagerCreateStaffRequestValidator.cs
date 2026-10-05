using FluentValidation;
using PetCare.Application.DTOs.Manager;

namespace PetCare.Application.Validators;

/// <summary>
/// Validates ClinicManager staff-creation requests (Veterinarian /
/// InventoryOfficer). The request intentionally has no OrganizationId —
/// the organization is resolved from the caller's tenant context in the
/// service layer.
/// </summary>
public class ManagerCreateStaffRequestValidator : AbstractValidator<ManagerCreateStaffRequest>
{
    public ManagerCreateStaffRequestValidator()
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

        RuleFor(r => r.PhoneNumber)
            .MaximumLength(50).WithMessage("Phone number must be 50 characters or fewer.");
    }
}
