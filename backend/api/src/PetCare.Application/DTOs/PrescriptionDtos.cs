using System.ComponentModel.DataAnnotations;

namespace PetCare.Application.DTOs;

/// <summary>One medicine line inside a vet's medicine request.</summary>
public class CreatePrescriptionItemDto
{
    [Required]
    public Guid MedicineId { get; set; }

    [Required]
    [StringLength(200)]
    public string Dosage { get; set; } = string.Empty;

    [Required]
    [Range(1, 365)]
    public int DurationDays { get; set; }

    /// <summary>Units of medicine being requested from inventory (≥ 1).</summary>
    [Range(1, 10000)]
    public int Quantity { get; set; } = 1;

    /// <summary>How often the dose is taken, e.g. "twice daily".</summary>
    [StringLength(100)]
    public string? Frequency { get; set; }

    /// <summary>Route/method of administration, e.g. "Oral", "Topical".</summary>
    [StringLength(100)]
    public string? Route { get; set; }

    /// <summary>Free-text administration instructions for the owner.</summary>
    [StringLength(500)]
    public string? Instructions { get; set; }
}

/// <summary>
/// One medicine request can hold 1–10 different medicines; each item is
/// stored as its own Prescription row under the same treatment record so
/// the inventory desk can issue/mark each line independently.
/// </summary>
public class CreatePrescriptionDto
{
    [Required]
    public Guid TreatmentRecordId { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "A medicine request must contain at least 1 medicine.")]
    [MaxLength(10, ErrorMessage = "A medicine request can contain at most 10 different medicines.")]
    public List<CreatePrescriptionItemDto> Items { get; set; } = new();
}

/// <summary>
/// Inventory officer request to mark a pending prescription as unavailable.
/// </summary>
public class MarkPrescriptionUnavailableRequest
{
    [Required]
    [StringLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public class PrescriptionResponseDto
{
    public Guid Id { get; set; }

    /// <summary>Human-readable prescription reference derived from the id.</summary>
    public string PrescriptionNumber => $"RX-{Id.ToString("N")[..8].ToUpperInvariant()}";

    public Guid TreatmentRecordId { get; set; }
    public Guid MedicineId { get; set; }
    public string Dosage { get; set; } = string.Empty;
    public int DurationDays { get; set; }
    public int Quantity { get; set; }
    public string? Frequency { get; set; }
    public string? Route { get; set; }
    public string? Instructions { get; set; }

    /// <summary>"Pending" | "Issued" | "Unavailable".</summary>
    public string RequestStatus { get; set; } = string.Empty;
    public string? UnavailableReason { get; set; }
    public Guid? ReservationId { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    // Denormalised display fields (walked up
    // Prescription -> TreatmentRecord -> Diagnosis -> Examination).
    public string? MedicineName { get; set; }
    public string? MedicineStrength { get; set; }
    public string? MedicineDosageForm { get; set; }
    public decimal? MedicineUnitPrice { get; set; }
    public string? PetId { get; set; }
    public string? PetName { get; set; }
    public string? OwnerName { get; set; }
    public Guid? VeterinarianId { get; set; }
    public string? VeterinarianName { get; set; }
    public Guid? ExaminationId { get; set; }
    public Guid? AppointmentId { get; set; }
    public decimal VeterinarianCharge { get; set; }
}
