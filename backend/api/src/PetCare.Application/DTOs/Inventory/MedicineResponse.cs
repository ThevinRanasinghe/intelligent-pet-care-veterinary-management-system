// MedicineResponse.cs
namespace PetCare.Application.DTOs.Inventory;

public class MedicineResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DosageForm { get; set; } = string.Empty;
    public string Strength { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public string Manufacturer { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int ReorderLevel { get; set; }
    public int TotalQuantity { get; set; }
    public int ReservedQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public bool IsLowStock { get; set; }
}