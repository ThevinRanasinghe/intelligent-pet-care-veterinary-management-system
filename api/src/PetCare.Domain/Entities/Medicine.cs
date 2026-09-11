using PetCare.Domain.Common;
using PetCare.Domain.Enums;

namespace PetCare.Domain.Entities;

public class Medicine : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DosageForm { get; set; } = string.Empty;
    public string Strength { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public string Manufacturer { get; set; } = string.Empty;
    public MedicineStatus Status { get; set; } = MedicineStatus.Active;
    public int ReorderLevel { get; set; }

    // Denormalized counters — always changed together, inside a
    // transaction, alongside the InventoryTransaction that explains why.
    public int TotalQuantity { get; set; }
    public int ReservedQuantity { get; set; }

    public int AvailableQuantity => TotalQuantity - ReservedQuantity;
    public bool IsLowStock => AvailableQuantity < ReorderLevel;

    public ICollection<MedicineBatch> Batches { get; set; } = new List<MedicineBatch>();
    public ICollection<MedicineReservation> Reservations { get; set; } = new List<MedicineReservation>();
}