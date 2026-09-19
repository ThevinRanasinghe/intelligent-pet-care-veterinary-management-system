// CreateMedicineRequest.cs
namespace PetCare.Application.DTOs.Inventory;

public class CreateMedicineRequest
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DosageForm { get; set; } = string.Empty;
    public string Strength { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public string Manufacturer { get; set; } = string.Empty;
    public int ReorderLevel { get; set; }
}