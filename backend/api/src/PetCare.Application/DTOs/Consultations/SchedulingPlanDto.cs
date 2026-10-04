using System.Text.Json.Serialization;

namespace PetCare.Application.DTOs.Consultations;

/// <summary>
/// Advisory AI scheduling/quotation proposal for a consultation request,
/// produced by the agentic scheduling agent. Mirrors the agent's validated
/// Pydantic response; <see cref="Source"/> is "agentic-ai" for a real
/// assessment or "unavailable" for the safe placeholder returned when the
/// agent cannot be used. Advisory only — the PetCare API remains the
/// authority for slot availability and conflict validation, and nothing
/// here is persisted or auto-applied.
/// </summary>
public class SchedulingPlanDto
{
    public string Source { get; set; } = "unavailable";

    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("recommendedAppointment")]
    public SchedulingAppointmentDto? RecommendedAppointment { get; set; }

    [JsonPropertyName("alternativeSlots")]
    public List<SchedulingAppointmentDto> AlternativeSlots { get; set; } = new();

    [JsonPropertyName("quotationProposal")]
    public SchedulingQuotationDto? QuotationProposal { get; set; }

    [JsonPropertyName("validationSummary")]
    public SchedulingValidationSummaryDto ValidationSummary { get; set; } = new();

    [JsonPropertyName("confidence")]
    public string Confidence { get; set; } = string.Empty;

    [JsonPropertyName("planningNotes")]
    public string PlanningNotes { get; set; } = string.Empty;

    [JsonPropertyName("disclaimer")]
    public string Disclaimer { get; set; } = string.Empty;
}

/// <summary>A slot candidate the agent proposes — always advisory.</summary>
public class SchedulingAppointmentDto
{
    [JsonPropertyName("appointmentSlotId")]
    public string AppointmentSlotId { get; set; } = string.Empty;

    [JsonPropertyName("veterinarianId")]
    public string VeterinarianId { get; set; } = string.Empty;

    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("startTime")]
    public string StartTime { get; set; } = string.Empty;

    [JsonPropertyName("endTime")]
    public string EndTime { get; set; } = string.Empty;

    [JsonPropertyName("branch")]
    public string Branch { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}

public class SchedulingQuotationItemDto
{
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("unitPrice")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}

public class SchedulingQuotationDto
{
    [JsonPropertyName("budget")]
    public decimal Budget { get; set; }

    [JsonPropertyName("items")]
    public List<SchedulingQuotationItemDto> Items { get; set; } = new();

    [JsonPropertyName("estimatedSubtotal")]
    public decimal EstimatedSubtotal { get; set; }

    [JsonPropertyName("estimatedTotal")]
    public decimal EstimatedTotal { get; set; }

    [JsonPropertyName("withinBudget")]
    public bool WithinBudget { get; set; }
}

/// <summary>The agent's self-check summary, verified against backend data.</summary>
public class SchedulingValidationSummaryDto
{
    [JsonPropertyName("slotFound")]
    public bool SlotFound { get; set; }

    [JsonPropertyName("veterinarianAvailable")]
    public bool VeterinarianAvailable { get; set; }

    [JsonPropertyName("noKnownConflict")]
    public bool NoKnownConflict { get; set; }

    [JsonPropertyName("withinRequestedTime")]
    public bool WithinRequestedTime { get; set; }

    [JsonPropertyName("withinBudget")]
    public bool WithinBudget { get; set; }
}
