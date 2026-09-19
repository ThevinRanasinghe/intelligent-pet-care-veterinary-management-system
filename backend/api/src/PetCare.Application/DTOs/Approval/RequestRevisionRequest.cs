namespace PetCare.Application.DTOs.Approval;

/// <summary>
/// Reason is required per the domain model rule "Reject / RevisionRequested
/// requires a reason".
/// </summary>
public class RequestRevisionRequest
{
    public Guid ReviewedBy { get; set; }

    public string Reason { get; set; } = string.Empty;
}
