using FluentValidation;
using PetCare.Application.DTOs.Admin;

namespace PetCare.Application.Validators;

/// <summary>
/// Validates Administrator staff-creation requests (Veterinarian /
/// InventoryOfficer). The role is not part of the request — it is assigned
/// by the endpoint — and OrganizationId is only format-checked here; the
/// service verifies the organization exists and is Active.
/// </summary>
public class CreateStaffUserRequestValidator : AbstractValidator<CreateStaffUserRequest>
{
    public CreateStaffUserRequestValidator()
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

        RuleFor(r => r.OrganizationId)
            .NotEqual(Guid.Empty).WithMessage("Organization is required.");
    }
}
