namespace PetCare.Application.DTOs.Consultations;

public class UpdateConsultationRequestDto
{
    public string Symptoms { get; set; } = string.Empty;

    public string? SymptomPhotoUrl { get; set; }

    public string Urgency { get; set; } = "Medium";

    public DateTime? PreferredDate { get; set; }

    public TimeSpan? PreferredTime { get; set; }

    public decimal? Budget { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public string? AdditionalNotes { get; set; }
}