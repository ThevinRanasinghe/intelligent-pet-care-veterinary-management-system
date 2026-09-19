namespace PetCare.Application.DTOs;

public class TreatmentRecommendationDto
{
    public string SuspectedCondition { get; set; } = string.Empty;
    public string RecommendedSeverity { get; set; } = "Moderate";
    public string Rationale { get; set; } = string.Empty;
    public List<string> RecommendedProcedures { get; set; } = new();
    public List<RecommendedMedicineDto> SuggestedMedicines { get; set; } = new();
    public List<string> PrecautionaryNotes { get; set; } = new();
}

public class RecommendedMedicineDto
{
    public Guid MedicineId { get; set; }
    public string MedicineName { get; set; } = string.Empty;
    public string SuggestedDosage { get; set; } = string.Empty;
    public int SuggestedDurationDays { get; set; }
}

public class CompleteTreatmentDto
{
    public string CompletionNotes { get; set; } = string.Empty;
}
