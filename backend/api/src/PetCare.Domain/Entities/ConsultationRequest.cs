namespace PetCare.Domain.Entities;

public class ConsultationRequest
{
    public string Id { get; set; } = string.Empty;

    public string PetId { get; set; } = string.Empty;

    public string OwnerId { get; set; } = string.Empty;

    public string SymptomsDescription { get; set; } = string.Empty;

    public string? PhotoUrl { get; set; }

    public DateTime PreferredDate { get; set; }

    public decimal BudgetLimit { get; set; }

    public double? PreferredClinicLocationLat { get; set; }

    public double? PreferredClinicLocationLong { get; set; }

    public string? PreferredBranch { get; set; }

    public string Status { get; set; } = "Draft";

    public string? StatusNotes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Pet Pet { get; set; } = null!;

    public PetOwner Owner { get; set; } = null!;

    public ICollection<ConsultationStatusHistory> StatusHistories { get; set; }
        = new List<ConsultationStatusHistory>();
}