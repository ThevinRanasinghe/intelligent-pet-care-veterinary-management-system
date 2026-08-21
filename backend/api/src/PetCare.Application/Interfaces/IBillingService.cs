using PetCare.Application.DTOs.Billing;

namespace PetCare.Application.Interfaces;

/// <summary>
/// Billing/Quotation use cases. See
/// docs/database/scheduling-billing-approval-domain-model.md#billing for the
/// underlying business rules and docs/api/scheduling-billing-approval-api-contract.md#billing
/// for the planned endpoints (controllers are added separately).
/// </summary>
public interface IBillingService
{
    Task<IReadOnlyList<QuotationResponse>> GetQuotationsAsync(CancellationToken cancellationToken = default);

    Task<QuotationResponse?> GetQuotationByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<QuotationResponse> CreateQuotationAsync(CreateQuotationRequest request, CancellationToken cancellationToken = default);

    Task<QuotationResponse> UpdateQuotationAsync(Guid id, UpdateQuotationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recomputes Subtotal/Total from the quotation's current line items,
    /// persists the result, and reports whether Total is within Budget.
    /// </summary>
    Task<QuotationResponse> CalculateQuotationAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a Draft/RevisionRequested quotation to PendingApproval. Blocked
    /// if Total exceeds Budget.
    /// </summary>
    Task<QuotationResponse> SubmitQuotationForApprovalAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Locks an Approved quotation as Finalised so its items/pricing can no
    /// longer be edited.
    /// </summary>
    Task<QuotationResponse> FinalizeQuotationAsync(Guid id, CancellationToken cancellationToken = default);
}
