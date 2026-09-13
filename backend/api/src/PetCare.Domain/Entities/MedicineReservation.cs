using PetCare.Domain.Common;
using PetCare.Domain.Enums;

namespace PetCare.Domain.Entities;

public class MedicineReservation : AuditableEntity
{
    public Guid MedicineId { get; set; }
    public Medicine Medicine { get; set; } = null!;

    public int Quantity { get; set; }

    public ReservationStatus Status { get; set; } = ReservationStatus.Reserved;

    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
}
