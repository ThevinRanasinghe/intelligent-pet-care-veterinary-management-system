namespace PetCare.Domain.Entities;

/// <summary>
/// A single line item on a Quotation.
/// See docs/database/scheduling-billing-approval-domain-model.md#quotationitem.
/// No CreatedAt/UpdatedAt per the domain model (line items are recomputed as a
/// unit whenever the quotation changes rather than individually audited).
/// Category is CHECK-constrained at the database level (Consultation,
/// Examination, Treatment, Medicine, Other) rather than modeled as a .NET enum.
/// </summary>
public class QuotationItem
{
    public Guid Id { get; set; }

    public Guid QuotationId { get; set; }

    public Quotation Quotation { get; set; } = null!;

    public string Category { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Must equal Quantity * UnitPrice; enforced via DB CHECK constraint and
    /// recomputed server-side before persisting.
    /// </summary>
    public decimal TotalPrice { get; set; }
}
