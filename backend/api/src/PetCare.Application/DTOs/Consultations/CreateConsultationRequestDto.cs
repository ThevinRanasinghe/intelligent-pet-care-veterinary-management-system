namespace PetCare.Application.DTOs.Consultations;

public class CreateConsultationRequestDto
{
    public string PetId { get; set; } = string.Empty;

    public string OwnerId { get; set; } = string.Empty;

    public string Symptoms { get; set; } = string.Empty;

    public string? SymptomPhotoUrl { get; set; }

    public string Urgency { get; set; } = "Medium";

    /// <summary>The clinic the owner is booking at (required).</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>Booking date (required, not in the past).</summary>
    public DateTime? PreferredDate { get; set; }

    /// <summary>
    /// One-hour slot start (required, hour-aligned, 09:00–17:00). The end
    /// time is always start + 1h — it is never client-entered.
    /// </summary>
    public TimeSpan? PreferredTime { get; set; }

    public decimal? Budget { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public string? AdditionalNotes { get; set; }
}