using System.Text.Json.Serialization;

namespace PetCare.Application.DTOs.Agentic;

/// <summary>
/// Mirrors the diagnosis agent's validated Pydantic response (advisory only).
/// The agent is not trusted to provide database MedicineIds — it returns
/// medicine names that must be resolved against the org-scoped catalogue.
/// </summary>
public class DiagnosisAgentResponse
{
    [JsonPropertyName("suspectedCondition")]
    public string? SuspectedCondition { get; set; }

    [JsonPropertyName("recommendedSeverity")]
    public string? RecommendedSeverity { get; set; }

    [JsonPropertyName("rationale")]
    public string? Rationale { get; set; }

    [JsonPropertyName("recommendedProcedures")]
    public List<string>? RecommendedProcedures { get; set; }

    [JsonPropertyName("suggestedMedicines")]
    public List<AgenticSuggestedMedicine>? SuggestedMedicines { get; set; }

    [JsonPropertyName("precautionaryNotes")]
    public List<string>? PrecautionaryNotes { get; set; }
}

public class AgenticSuggestedMedicine
{
    [JsonPropertyName("medicineName")]
    public string? MedicineName { get; set; }

    [JsonPropertyName("suggestedDosage")]
    public string? SuggestedDosage { get; set; }

    [JsonPropertyName("suggestedDurationDays")]
    public int SuggestedDurationDays { get; set; }
}
