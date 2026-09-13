using PetCare.Domain.Enums;

namespace PetCare.Domain.Entities;

public class InventoryTransaction
{
    public Guid Id { get; set; }

    public Guid MedicineId { get; set; }
    public Medicine Medicine { get; set; } = null!;

    public Guid? BatchId { get; set; }
    public MedicineBatch? Batch { get; set; }

    public Guid? ReservationId { get; set; }
    public MedicineReservation? Reservation { get; set; }

    public InventoryTransactionType Type { get; set; }

    /// <summary>Signed: negative for dispense/damage/expiry, positive for stock-in/return.</summary>
    public int QuantityChange { get; set; }

    public Guid PerformedByUserId { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}