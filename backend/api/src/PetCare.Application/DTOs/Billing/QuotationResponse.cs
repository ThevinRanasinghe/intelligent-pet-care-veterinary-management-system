namespace PetCare.Application.DTOs.Billing;

public class QuotationResponse
{
    public Guid Id { get; set; }

    public Guid AppointmentId { get; set; }

    public decimal Budget { get; set; }

    public decimal Subtotal { get; set; }

    public decimal Total { get; set; }

    /// <summary>
    /// True when Total &lt;= Budget. Computed on every response so clients
    /// never need to duplicate the comparison themselves.
    /// </summary>
    public bool IsWithinBudget { get; set; }

    public string Status { get; set; } = string.Empty;

    public List<QuotationItemResponse> Items { get; set; } = new();

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
