namespace PetCare.Application.DTOs.Consultations;

public class ConsultationRequestDto
{
    public string Id { get; set; } = string.Empty;

    public string PetId { get; set; } = string.Empty;

    public string OwnerId { get; set; } = string.Empty;

    public string PetName { get; set; } = string.Empty;

    public string Symptoms { get; set; } = string.Empty;

    public string? SymptomPhotoUrl { get; set; }

    public string Urgency { get; set; } = string.Empty;

    public DateTime? PreferredDate { get; set; }

    public TimeSpan? PreferredTime { get; set; }

    public decimal? Budget { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public string? AdditionalNotes { get; set; }

    public string Status { get; set; } = string.Empty;

    /// <summary>The clinic the consultation is booked at (null for legacy rows).</summary>
    public Guid? OrganizationId { get; set; }

    public string? OrganizationName { get; set; }

    /// <summary>"Initial" (owner-filed) | "FollowUp" (vet-requested).</summary>
    public string RequestType { get; set; } = "Initial";

    public Guid? RequestedByVeterinarianId { get; set; }

    public string? RequestedByVeterinarianName { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}