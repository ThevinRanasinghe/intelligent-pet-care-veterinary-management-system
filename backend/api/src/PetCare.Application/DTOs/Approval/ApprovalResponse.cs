namespace PetCare.Application.DTOs.Approval;

/// <summary>
/// The current approval decision for a quotation, plus enough quotation
/// context (Total/Budget) for a reviewer to make a decision without a
/// separate round trip to the Billing endpoints.
/// </summary>
public class ApprovalResponse
{
    public Guid Id { get; set; }

    public Guid QuotationId { get; set; }

    public decimal QuotationTotal { get; set; }

    public decimal QuotationBudget { get; set; }

    public string Status { get; set; } = string.Empty;

    public Guid? ReviewedBy { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public string? Comment { get; set; }
}
