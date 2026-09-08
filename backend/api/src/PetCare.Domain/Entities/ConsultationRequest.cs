using PetCare.Domain.Enums;

namespace PetCare.Domain.Entities;

public class ConsultationRequest
{
    public string Id { get; set; } = string.Empty; // e.g., REQ-5001
    public string PetId { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public string SymptomsDescription { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public DateTime PreferredDate { get; set; }
    public decimal BudgetLimit { get; set; }
    
    // Geographic preference (Lat/Long coordinates)
    public double? PreferredClinicLocationLat { get; set; }
    public double? PreferredClinicLocationLong { get; set; }
    public string? PreferredBranch { get; set; }
    
    // Workflow Status
    public ConsultationStatus Status { get; set; } = ConsultationStatus.Submitted;
    public string? StatusNotes { get; set; }
    
    // Audit fields
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Pet? Pet { get; set; }
    public PetOwner? Owner { get; set; }
    public ICollection<ConsultationStatusHistory> StatusHistories { get; set; } = new List<ConsultationStatusHistory>();
}