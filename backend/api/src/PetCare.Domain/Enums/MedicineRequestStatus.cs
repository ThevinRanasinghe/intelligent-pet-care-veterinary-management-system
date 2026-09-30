namespace PetCare.Domain.Enums;

/// <summary>
/// Fulfillment state of a <see cref="Entities.Prescription"/> treated as a
/// medicine request to the inventory desk. Stored as a string.
/// </summary>
public enum MedicineRequestStatus
{
    /// <summary>Requested by the veterinarian; awaiting the inventory officer.</summary>
    Pending,

    /// <summary>Stock was reserved and dispensed against this prescription.</summary>
    Issued,

    /// <summary>The inventory officer marked the medicine as unavailable.</summary>
    Unavailable
}
