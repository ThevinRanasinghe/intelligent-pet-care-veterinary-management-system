namespace PetCare.Application.DTOs.Approval;

public class ApprovalHistoryResponse
{
    public Guid Id { get; set; }

    public Guid ApprovalId { get; set; }

    public string PreviousStatus { get; set; } = string.Empty;

    public string NewStatus { get; set; } = string.Empty;

    public Guid ChangedBy { get; set; }

    public string? Reason { get; set; }

    public DateTimeOffset ChangedAt { get; set; }
}
