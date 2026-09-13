// ReservationResponse.cs
namespace PetCare.Application.DTOs.Inventory;

public class ReservationResponse
{
    public Guid Id { get; set; }
    public Guid MedicineId { get; set; }
    public int Quantity { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
}