// InventoryTransactionResponse.cs — needed by the transaction-history
// endpoint in Day 2 step 7, defined here since that's where the rest of
// the Inventory DTOs live.
namespace PetCare.Application.DTOs.Inventory;

public class InventoryTransactionResponse
{
    public Guid Id { get; set; }
    public Guid MedicineId { get; set; }
    public Guid? BatchId { get; set; }
    public Guid? ReservationId { get; set; }
    public string Type { get; set; } = string.Empty;
    public int QuantityChange { get; set; }
    public Guid PerformedByUserId { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}