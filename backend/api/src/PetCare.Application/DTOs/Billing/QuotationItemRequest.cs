namespace PetCare.Application.DTOs.Billing;

/// <summary>
/// Client-supplied line item input. TotalPrice is intentionally absent: it is
/// always recomputed server-side as Quantity * UnitPrice per the "backend is
/// authoritative for final quotation calculation" business rule.
/// </summary>
public class QuotationItemRequest
{
    public string Category { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }
}
