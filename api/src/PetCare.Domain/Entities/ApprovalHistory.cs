namespace PetCare.Domain.Entities;

/// <summary>
/// Immutable audit history row for approval status transitions.
/// </summary>
public class ApprovalHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ApprovalId { get; set; }

    public Approval Approval { get; set; } = null!;

    public string PreviousStatus { get; set; } = string.Empty;

    public string NewStatus { get; set; } = string.Empty;

    public Guid ChangedBy { get; set; }

    public string? Reason { get; set; }

    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;
}
