// ReservationResponse.cs
namespace PetCare.Application.DTOs.Inventory;

public class ReservationResponse
{
    public Guid Id { get; set; }
    public Guid MedicineId { get; set; }
    public string? MedicineName { get; set; }
    public int Quantity { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}