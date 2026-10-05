namespace PetCare.Domain.Enums;

/// <summary>
/// Payment state of a finalised <see cref="Entities.Quotation"/> (bill).
/// Stored as a string in the database.
/// </summary>
public enum PaymentStatus
{
    Pending,
    Paid
}
