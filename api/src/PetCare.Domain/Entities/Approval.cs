using PetCare.Domain.Enums;

namespace PetCare.Domain.Entities;

/// <summary>
/// A manager approval checkpoint for a quotation / AI proposal.
/// </summary>
public class Approval
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid QuotationId { get; set; }

    public Quotation Quotation { get; set; } = null!;

    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;

    public Guid? ReviewedBy { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public string? Comment { get; set; }

    public ICollection<ApprovalHistory> Histories { get; set; } = new List<ApprovalHistory>();
}
