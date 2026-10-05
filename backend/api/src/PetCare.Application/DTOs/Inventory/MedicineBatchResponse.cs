// MedicineBatchResponse.cs
namespace PetCare.Application.DTOs.Inventory;

public class MedicineBatchResponse
{
    public Guid Id { get; set; }
    public Guid MedicineId { get; set; }
    public Guid SupplierId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public DateOnly ExpiryDate { get; set; }
    public DateOnly ReceivedDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsExpired { get; set; }
}