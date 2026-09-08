using PetCare.Domain.Common;
using PetCare.Domain.Enums;

namespace PetCare.Domain.Entities;

/// <summary>
/// A billing quotation for an appointment, composed of line items.
/// See docs/database/scheduling-billing-approval-domain-model.md#quotation.
/// Subtotal/Total are recomputed server-side from QuotationItem rows and must
/// never be trusted directly from client input.
/// </summary>
public class Quotation : AuditableEntity
{
    /// <summary>
    /// UNIQUE FK: one appointment has exactly one quotation.
    /// </summary>
    public Guid AppointmentId { get; set; }

    public Appointment Appointment { get; set; } = null!;

    public decimal Budget { get; set; }

    public decimal Subtotal { get; set; } = 0m;

    public decimal Total { get; set; } = 0m;

    public QuotationStatus Status { get; set; } = QuotationStatus.Draft;

    public ICollection<QuotationItem> Items { get; set; } = new List<QuotationItem>();

    /// <summary>
    /// 1:1 with Approval (Approval.QuotationId is the UNIQUE FK owner side).
    /// </summary>
    public Approval? Approval { get; set; }
}
