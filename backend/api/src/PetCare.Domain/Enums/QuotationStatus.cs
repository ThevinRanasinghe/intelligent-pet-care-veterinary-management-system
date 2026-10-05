namespace PetCare.Domain.Enums;

/// <summary>
/// Status of a <see cref="Entities.Quotation"/>.
/// Mirrors the frontend QuotationStatus type (frontend/web/src/types/domain.ts).
/// </summary>
public enum QuotationStatus
{
    Draft,
    PendingApproval,
    Approved,
    Rejected,
    RevisionRequested,
    Finalised
}
