namespace PetCare.Application.DTOs.Billing;

/// <summary>
/// Replaces a quotation's Budget and full line item set. AppointmentId is
/// immutable after creation and therefore not part of this request.
/// </summary>
public class UpdateQuotationRequest
{
    public decimal Budget { get; set; }

    public List<QuotationItemRequest> Items { get; set; } = new();
}
