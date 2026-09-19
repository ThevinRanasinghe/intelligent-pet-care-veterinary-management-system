// ReceiveStockRequest.cs
namespace PetCare.Application.DTOs.Inventory;

public class ReceiveStockRequest
{
    public Guid SupplierId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public DateOnly ExpiryDate { get; set; }
}