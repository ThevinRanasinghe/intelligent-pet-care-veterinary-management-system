using PetCare.Domain.Enums;

namespace PetCare.Domain.Entities;

public class Prescription
{
    public Guid Id { get; set; }
    public Guid TreatmentRecordId { get; set; }
    public TreatmentRecord? TreatmentRecord { get; set; }

    public Guid MedicineId { get; set; }
    public Medicine? Medicine { get; set; }

    public string Dosage { get; set; } = string.Empty;
    public int DurationDays { get; set; }

    /// <summary>Units of the medicine being requested (drives the reservation
    /// quantity and the bill line amount).</summary>
    public int Quantity { get; set; } = 1;

    /// <summary>How often the dose is taken, e.g. "twice daily".</summary>
    public string? Frequency { get; set; }

    /// <summary>Route/method of administration recorded by the vet, e.g. "Oral", "Topical".</summary>
    public string? Route { get; set; }

    /// <summary>Free-text administration instructions for the owner.</summary>
    public string? Instructions { get; set; }

    /// <summary>Fulfillment state of this prescription as a medicine request.</summary>
    public MedicineRequestStatus RequestStatus { get; set; } = MedicineRequestStatus.Pending;

    /// <summary>Why the inventory officer marked the request Unavailable.</summary>
    public string? UnavailableReason { get; set; }

    /// <summary>The reservation that satisfied this request (set on issue).</summary>
    public Guid? ReservationId { get; set; }
    public MedicineReservation? Reservation { get; set; }

    /// <summary>The inventory officer / admin who processed the request.</summary>
    public Guid? ProcessedByUserId { get; set; }
    public User? ProcessedBy { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
