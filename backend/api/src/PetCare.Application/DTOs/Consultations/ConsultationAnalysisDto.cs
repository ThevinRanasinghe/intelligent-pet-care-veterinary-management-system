using System.Text.Json.Serialization;

namespace PetCare.Application.DTOs.Consultations;

/// <summary>
/// Advisory AI triage of a consultation request, produced by the agentic
/// consultation agent. Mirrors the agent's validated Pydantic response;
/// <see cref="Source"/> is "agentic-ai" for a real assessment or
/// "unavailable" for the safe placeholder returned when the agent cannot
/// be used. Advisory only — it never changes request state.
/// </summary>
public class ConsultationAnalysisDto
{
    public string Source { get; set; } = "unavailable";

    [JsonPropertyName("consultationRequestId")]
    public string ConsultationRequestId { get; set; } = string.Empty;

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = string.Empty;

    [JsonPropertyName("consultationType")]
    public string ConsultationType { get; set; } = string.Empty;

    [JsonPropertyName("keyConcerns")]
    public List<ConsultationKeyConcernDto> KeyConcerns { get; set; } = new();

    [JsonPropertyName("recommendedChecks")]
    public List<string> RecommendedChecks { get; set; } = new();

    [JsonPropertyName("suggestedNextStep")]
    public string SuggestedNextStep { get; set; } = string.Empty;

    [JsonPropertyName("disclaimer")]
    public string Disclaimer { get; set; } = string.Empty;
}

public class ConsultationKeyConcernDto
{
    [JsonPropertyName("concern")]
    public string Concern { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}
