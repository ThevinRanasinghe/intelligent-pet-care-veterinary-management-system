namespace PetCare.Application.DTOs.Billing;

public class QuotationResponse
{
    public Guid Id { get; set; }

    /// <summary>Human-readable invoice reference derived from the quotation id.</summary>
    public string InvoiceNumber => $"INV-{Id.ToString("N")[..8].ToUpperInvariant()}";

    public Guid AppointmentId { get; set; }

    public decimal Budget { get; set; }

    public decimal Subtotal { get; set; }

    public decimal Total { get; set; }

    /// <summary>
    /// True when Total &lt;= Budget. Computed on every response so clients
    /// never need to duplicate the comparison themselves.
    /// </summary>
    public bool IsWithinBudget { get; set; }

    public string Status { get; set; } = string.Empty;

    /// <summary>"Pending" | "Paid".</summary>
    public string PaymentStatus { get; set; } = string.Empty;

    public DateTimeOffset? PaidAt { get; set; }

    public List<QuotationItemResponse> Items { get; set; } = new();

    // Denormalised display/billing fields (populated via repository
    // includes of Appointment -> Pet -> Owner, Appointment -> Veterinarian
    // and Appointment -> Examination).
    public DateOnly? AppointmentDate { get; set; }

    public TimeOnly? AppointmentStartTime { get; set; }

    public TimeOnly? AppointmentEndTime { get; set; }

    /// <summary>Clinic (organization) name, via the appointment's veterinarian.</summary>
    public string? ClinicName { get; set; }

    public string? PetId { get; set; }

    public string? PetName { get; set; }

    public string? OwnerId { get; set; }

    public string? OwnerName { get; set; }

    public string? OwnerEmail { get; set; }

    public string? OwnerPhone { get; set; }

    public Guid? VeterinarianId { get; set; }

    public string? VeterinarianName { get; set; }

    public Guid? ExaminationId { get; set; }

    public DateTime? ExaminationDate { get; set; }

    /// <summary>Sum of Examination-category line totals.</summary>
    public decimal VeterinarianChargeTotal { get; set; }

    /// <summary>Sum of Medicine-category line totals.</summary>
    public decimal MedicineTotal { get; set; }

    /// <summary>
    /// Every medicine prescribed under the linked examination — Pending,
    /// Issued or Unavailable — with the veterinarian's administration
    /// instructions (dosage/frequency/route/duration/instructions). Only
    /// Issued medicines are billed; the rest are shown so the owner can see
    /// the full prescription and each item's fulfillment status.
    /// </summary>
    public List<PrescriptionResponseDto> Medications { get; set; } = new();

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
