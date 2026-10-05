using PetCare.Domain.Common;
using PetCare.Domain.Enums;

namespace PetCare.Domain.Entities;

public class MedicineBatch : AuditableEntity
{
    public Guid MedicineId { get; set; }
    public Medicine Medicine { get; set; } = null!;

    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;

    public string BatchNumber { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public DateOnly ExpiryDate { get; set; }
    public DateOnly ReceivedDate { get; set; }
    public BatchStatus Status { get; set; } = BatchStatus.Active;

    public bool IsExpired => ExpiryDate <= DateOnly.FromDateTime(DateTime.UtcNow);
    public bool IsUsable => Status == BatchStatus.Active && Quantity > 0 && !IsExpired;
}