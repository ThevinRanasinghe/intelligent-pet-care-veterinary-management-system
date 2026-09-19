namespace PetCare.Application.DTOs.Billing;

public class CreateQuotationRequest
{
    /// <summary>
    /// UNIQUE FK: the appointment this quotation belongs to. One appointment
    /// has exactly one quotation.
    /// </summary>
    public Guid AppointmentId { get; set; }

    public decimal Budget { get; set; }

    public List<QuotationItemRequest> Items { get; set; } = new();
}
