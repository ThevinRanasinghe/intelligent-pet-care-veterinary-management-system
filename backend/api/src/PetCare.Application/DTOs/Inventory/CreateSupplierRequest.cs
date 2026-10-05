// CreateSupplierRequest.cs
namespace PetCare.Application.DTOs.Inventory;

public class CreateSupplierRequest
{
    public string Name { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}