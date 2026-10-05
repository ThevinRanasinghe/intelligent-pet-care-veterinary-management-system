namespace PetCare.Application.DTOs.Billing;

public class QuotationItemResponse
{
    public Guid Id { get; set; }

    public string Category { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal TotalPrice { get; set; }
}
