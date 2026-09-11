namespace PetCare.Domain.Entities;

/// <summary>
/// A consultation request submitted by a pet owner.
/// </summary>
public class ConsultationRequest
{
    public string Id { get; set; } = string.Empty;

    public Guid? OrganizationId { get; set; }

    public Organization? Organization { get; set; }

    public string PetId { get; set; } = string.Empty;

    public Pet? Pet { get; set; }

    public string OwnerId { get; set; } = string.Empty;

    public PetOwner? Owner { get; set; }

    public string SymptomsDescription { get; set; } = string.Empty;

    public string? PhotoUrl { get; set; }

    public DateTime PreferredDate { get; set; }

    public decimal BudgetLimit { get; set; }

    public double? PreferredClinicLocationLat { get; set; }

    public double? PreferredClinicLocationLong { get; set; }

    public string? PreferredBranch { get; set; }

    public string Status { get; set; } = "Submitted";

    public string? StatusNotes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
