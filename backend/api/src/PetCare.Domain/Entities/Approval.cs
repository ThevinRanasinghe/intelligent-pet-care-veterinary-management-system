using PetCare.Domain.Enums;

namespace PetCare.Domain.Entities;

/// <summary>
/// The current approval decision for a Quotation.
/// See docs/database/scheduling-billing-approval-domain-model.md#approval.
/// No CreatedAt/UpdatedAt per the domain model; ReviewedAt captures the
/// decision timestamp and every status change is mirrored into ApprovalHistory.
/// </summary>
public class Approval
{
    public Guid Id { get; set; }

    /// <summary>
    /// UNIQUE FK: one quotation has exactly one current approval record.
    /// </summary>
    public Guid QuotationId { get; set; }

    public Quotation Quotation { get; set; } = null!;

    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;

    /// <summary>
    /// FK to the reviewing Clinic Manager (User entity owned by another module).
    /// </summary>
    public Guid? ReviewedBy { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public string? Comment { get; set; }

    public ICollection<ApprovalHistory> History { get; set; } = new List<ApprovalHistory>();
}
