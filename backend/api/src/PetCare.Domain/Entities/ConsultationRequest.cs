namespace PetCare.Domain.Entities;

public class ConsultationRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PetId { get; set; }
    public Guid OwnerId { get; set; }
    public string SymptomsDescription { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public string PreferredBranch { get; set; } = string.Empty;
    public DateTime PreferredDate { get; set; }
    public decimal BudgetLimit { get; set; }
    
    // Workflow Status
    public string Status { get; set; } = "Pending"; // Pending, PendingManagerApproval, Approved, Rejected
    
    // Audit fields
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public Pet? Pet { get; set; }
}