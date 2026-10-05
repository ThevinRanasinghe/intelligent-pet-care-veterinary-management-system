// ReserveMedicineRequest.cs
namespace PetCare.Application.DTOs.Inventory;

public class ReserveMedicineRequest
{
    public Guid MedicineId { get; set; }
    public int Quantity { get; set; }

    // What triggered this reservation — "Appointment" | "Treatment" | "Prescription".
    // Optional: not every reservation traces back to one of those yet.
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
}