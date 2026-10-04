using System.Text.Json.Serialization;

namespace PetCare.Application.DTOs.Inventory;

/// <summary>
/// Advisory AI inventory/medicine recommendation for a medicine request,
/// produced by the agentic inventory agent. Mirrors the agent's validated
/// Pydantic response (keyed by treatment record id — a medicine request is
/// the batch of prescription rows under that treatment record).
/// <see cref="Source"/> is "agentic-ai" for a real assessment or
/// "unavailable" for the safe placeholder returned when the agent cannot
/// be used. Advisory only — the PetCare API remains the authority for
/// stock, batches, and issue validation; nothing here is persisted or
/// auto-applied and no inventory operation is performed.
/// </summary>
public class InventoryPlanDto
{
    public string Source { get; set; } = "unavailable";

    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("medicineRecommendation")]
    public InventoryMedicineRecommendationDto? MedicineRecommendation { get; set; }

    [JsonPropertyName("recommendedBatch")]
    public InventoryRecommendedBatchDto? RecommendedBatch { get; set; }

    /// <summary>
    /// Informational alternatives only — never an automatic substitution.
    /// Any change to the prescribed medicine requires the veterinarian.
    /// </summary>
    [JsonPropertyName("alternativeMedicines")]
    public List<InventoryAlternativeMedicineDto> AlternativeMedicines { get; set; } = new();

    [JsonPropertyName("inventorySummary")]
    public InventoryPlanSummaryDto InventorySummary { get; set; } = new();

    [JsonPropertyName("confidence")]
    public string Confidence { get; set; } = string.Empty;

    [JsonPropertyName("planningNotes")]
    public string PlanningNotes { get; set; } = string.Empty;

    [JsonPropertyName("disclaimer")]
    public string Disclaimer { get; set; } = string.Empty;
}

/// <summary>
/// The medicine the agent proposes issuing — facts (name, available
/// quantity, sufficientStock) are overwritten from the real catalogue by
/// the agent's validation node, not trusted from the LLM.
/// </summary>
public class InventoryMedicineRecommendationDto
{
    [JsonPropertyName("medicineId")]
    public string MedicineId { get; set; } = string.Empty;

    [JsonPropertyName("medicineName")]
    public string MedicineName { get; set; } = string.Empty;

    [JsonPropertyName("requiredQuantity")]
    public int RequiredQuantity { get; set; }

    [JsonPropertyName("availableQuantity")]
    public int AvailableQuantity { get; set; }

    [JsonPropertyName("sufficientStock")]
    public bool SufficientStock { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// The batch the agent proposes issuing from — batch number, quantity and
/// expiry are overwritten from the real batch record, and expired or
/// fabricated batches are rejected upstream.
/// </summary>
public class InventoryRecommendedBatchDto
{
    [JsonPropertyName("batchId")]
    public string BatchId { get; set; } = string.Empty;

    [JsonPropertyName("batchNumber")]
    public string BatchNumber { get; set; } = string.Empty;

    [JsonPropertyName("quantityAvailable")]
    public int QuantityAvailable { get; set; }

    [JsonPropertyName("expiryDate")]
    public string ExpiryDate { get; set; } = string.Empty;

    [JsonPropertyName("expiryStatus")]
    public string ExpiryStatus { get; set; } = string.Empty;
}

/// <summary>
/// A possible alternative medicine — informational only. Tolerant shape:
/// the agent's schema leaves alternative items loosely typed, so all
/// fields are optional and unknown properties are ignored.
/// </summary>
public class InventoryAlternativeMedicineDto
{
    [JsonPropertyName("medicineId")]
    public string? MedicineId { get; set; }

    [JsonPropertyName("medicineName")]
    public string? MedicineName { get; set; }

    [JsonPropertyName("availableQuantity")]
    public int? AvailableQuantity { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

/// <summary>The agent's self-check summary, verified against real backend data.</summary>
public class InventoryPlanSummaryDto
{
    [JsonPropertyName("medicineFound")]
    public bool MedicineFound { get; set; }

    [JsonPropertyName("stockAvailable")]
    public bool StockAvailable { get; set; }

    [JsonPropertyName("sufficientQuantity")]
    public bool SufficientQuantity { get; set; }

    [JsonPropertyName("batchAvailable")]
    public bool BatchAvailable { get; set; }

    [JsonPropertyName("notExpired")]
    public bool NotExpired { get; set; }

    [JsonPropertyName("lowStock")]
    public bool LowStock { get; set; }
}
