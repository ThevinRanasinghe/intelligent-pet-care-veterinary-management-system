using FluentValidation;
using PetCare.Application.DTOs.Billing;
using PetCare.Application.Interfaces;

namespace PetCare.Application.Validators;

/// <summary>
/// Line items allowed per docs/database/scheduling-billing-approval-domain-model.md
/// (CK_QuotationItem_Category_Allowed database check constraint).
/// </summary>
internal static class QuotationCategories
{
    public static readonly string[] Allowed = { "Consultation", "Examination", "Treatment", "Medicine", "Other" };
}

/// <summary>
/// Structural/referential validation for CreateQuotationRequest: required
/// fields, non-negative Budget, existence of the referenced appointment, the
/// 1:1 Appointment&lt;-&gt;Quotation relationship, and per-item rules (Quantity &gt; 0,
/// UnitPrice &gt;= 0, known Category). TotalPrice/Subtotal/Total are never
/// accepted from the client and are always recomputed by BillingService.
/// </summary>
public class CreateQuotationRequestValidator : AbstractValidator<CreateQuotationRequest>
{
    public CreateQuotationRequestValidator(
        IAppointmentRepository appointmentRepository,
        IQuotationRepository quotationRepository)
    {
        RuleFor(r => r.AppointmentId)
            .NotEmpty()
            .MustAsync(async (id, cancellationToken) =>
                await appointmentRepository.GetByIdAsync(id, cancellationToken) is not null)
            .WithMessage("Appointment does not exist.");

        RuleFor(r => r.AppointmentId)
            .MustAsync(async (id, cancellationToken) =>
                !await quotationRepository.ExistsForAppointmentAsync(id, cancellationToken))
            .WithMessage("A quotation already exists for this appointment.")
            .When(r => r.AppointmentId != Guid.Empty);

        RuleFor(r => r.Budget)
            .GreaterThanOrEqualTo(0m)
            .WithMessage("Budget must be greater than or equal to 0.");

        RuleForEach(r => r.Items).SetValidator(new QuotationItemRequestValidator());
    }
}

/// <summary>
/// Structural validation for UpdateQuotationRequest (Budget and per-item
/// rules). AppointmentId cannot change after creation so it is not validated
/// here.
/// </summary>
public class UpdateQuotationRequestValidator : AbstractValidator<UpdateQuotationRequest>
{
    public UpdateQuotationRequestValidator()
    {
        RuleFor(r => r.Budget)
            .GreaterThanOrEqualTo(0m)
            .WithMessage("Budget must be greater than or equal to 0.");

        RuleForEach(r => r.Items).SetValidator(new QuotationItemRequestValidator());
    }
}

/// <summary>
/// Shared per-line-item rules: Quantity &gt; 0, UnitPrice &gt;= 0, Description
/// required, Category must be one of the allowed values.
/// </summary>
public class QuotationItemRequestValidator : AbstractValidator<QuotationItemRequest>
{
    public QuotationItemRequestValidator()
    {
        RuleFor(i => i.Description)
            .NotEmpty()
            .WithMessage("Description is required.");

        RuleFor(i => i.Quantity)
            .GreaterThan(0)
            .WithMessage("Quantity must be greater than 0.");

        RuleFor(i => i.UnitPrice)
            .GreaterThanOrEqualTo(0m)
            .WithMessage("UnitPrice must be greater than or equal to 0.");

        RuleFor(i => i.Category)
            .Must(category => QuotationCategories.Allowed.Contains(category))
            .WithMessage($"Category must be one of: {string.Join(", ", QuotationCategories.Allowed)}.");
    }
}
