using PetCare.Domain.Enums;

namespace PetCare.Domain.Entities;

/// <summary>
/// Immutable audit trail row for every status change on an Approval.
/// See docs/database/scheduling-billing-approval-domain-model.md#approvalhistory.
/// Inserted in the same transaction as the Approval.Status change it records.
/// </summary>
public class ApprovalHistory
{
    public Guid Id { get; set; }

    public Guid ApprovalId { get; set; }

    public Approval Approval { get; set; } = null!;

    public ApprovalStatus PreviousStatus { get; set; }

    public ApprovalStatus NewStatus { get; set; }

    /// <summary>
    /// FK to the User who made the change (User entity owned by another module).
    /// </summary>
    public Guid ChangedBy { get; set; }

    public string? Reason { get; set; }

    public DateTimeOffset ChangedAt { get; set; }
}
