namespace PetCare.Application.DTOs.Consultations;

public class UpdateConsultationRequestDto
{
    public string Symptoms { get; set; } = string.Empty;

    public string? SymptomPhotoUrl { get; set; }

    public string Urgency { get; set; } = "Medium";

    /// <summary>Move the request to a different clinic (optional).</summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>New booking date — when supplied, PreferredTime must also be
    /// supplied and the pair follows the same booking rules as create.</summary>
    public DateTime? PreferredDate { get; set; }

    /// <summary>New one-hour slot start (hour-aligned, 09:00–17:00).</summary>
    public TimeSpan? PreferredTime { get; set; }

    public decimal? Budget { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public string? AdditionalNotes { get; set; }
}