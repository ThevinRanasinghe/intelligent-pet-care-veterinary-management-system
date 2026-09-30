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

    /// <summary>
    /// Creates or refreshes the bill (quotation) for the appointment behind
    /// an examination: one Examination line for the veterinarian charge plus
    /// one Medicine line per Issued prescription. Returns null without doing
    /// anything when the examination is not linked to an appointment, and
    /// leaves a Paid bill untouched.
    /// </summary>
    Task<QuotationResponse?> GenerateOrRefreshBillForExaminationAsync(
        Guid examinationId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a Finalised, unpaid quotation as Paid (InventoryOfficer/Admin).
    /// </summary>
    Task<QuotationResponse> MarkPaidAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Quotations for appointments of pets owned by the given owner id —
    /// the PetOwner "my bills" view (not organization-scoped).
    /// </summary>
    Task<IReadOnlyList<QuotationResponse>> GetQuotationsForOwnerAsync(
        string ownerId,
        CancellationToken cancellationToken = default);
}
