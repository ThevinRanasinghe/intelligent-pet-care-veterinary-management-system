using PetCare.Domain.Common;
using PetCare.Domain.Enums;

namespace PetCare.Domain.Entities;

/// <summary>
/// A quotation for an appointment with associated line items and budget limits.
/// </summary>
public class Quotation : AuditableEntity
{
    public Guid AppointmentId { get; set; }

    public Appointment Appointment { get; set; } = null!;

    public decimal Budget { get; set; }

    public decimal Subtotal { get; set; }

    public decimal Total { get; set; }

    public QuotationStatus Status { get; set; } = QuotationStatus.Draft;

    public ICollection<QuotationItem> Items { get; set; } = new List<QuotationItem>();

    public Approval? Approval { get; set; }
}
