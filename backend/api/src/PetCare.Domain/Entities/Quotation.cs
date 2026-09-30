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

    /// <summary>Payment state of the bill once it is Finalised.</summary>
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

    public DateTimeOffset? PaidAt { get; set; }

    /// <summary>The staff account (InventoryOfficer/Admin) that recorded payment.</summary>
    public Guid? PaidByUserId { get; set; }

    public User? PaidBy { get; set; }

    public ICollection<QuotationItem> Items { get; set; } = new List<QuotationItem>();

    /// <summary>
    /// 1:1 with Approval (Approval.QuotationId is the UNIQUE FK owner side).
    /// </summary>
    public Approval? Approval { get; set; }
}
