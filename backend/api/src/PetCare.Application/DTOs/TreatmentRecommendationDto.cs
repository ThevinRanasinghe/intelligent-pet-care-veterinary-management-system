namespace PetCare.Application.DTOs;

public class TreatmentRecommendationDto
{
    public string SuspectedCondition { get; set; } = string.Empty;
    public string RecommendedSeverity { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    public List<string> RecommendedProcedures { get; set; } = new();
    public List<RecommendedMedicineDto> SuggestedMedicines { get; set; } = new();
    public List<string> PrecautionaryNotes { get; set; } = new();

    /// <summary>
    /// Provenance of the recommendation: "agentic-ai" when produced by the AI
    /// service, "unavailable" when the AI call failed and this is a safe
    /// placeholder the vet can dismiss. Never persisted — advisory only.
    /// </summary>
    public string Source { get; set; } = "agentic-ai";
}

public class RecommendedMedicineDto
{
    /// <summary>
    /// Nullable: only populated when the AI-suggested medicine name resolved to
    /// exactly one medicine in the caller's organization catalogue. Null means
    /// "name is advisory only — not matched to a formulary record". The AI is
    /// never trusted to provide database ids.
    /// </summary>
    public Guid? MedicineId { get; set; }
    public string MedicineName { get; set; } = string.Empty;
    public string SuggestedDosage { get; set; } = string.Empty;
    public int SuggestedDurationDays { get; set; }
}
