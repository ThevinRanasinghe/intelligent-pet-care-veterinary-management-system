namespace PetCare.Domain.Entities;

/// <summary>
/// A line item within a Quotation (Consultation, Examination, Treatment, Medicine, Other).
/// </summary>
public class QuotationItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid QuotationId { get; set; }

    public Quotation Quotation { get; set; } = null!;

    public string Category { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal TotalPrice { get; set; }
}
