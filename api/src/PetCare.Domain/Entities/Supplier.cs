using PetCare.Domain.Common;
using PetCare.Domain.Enums;

namespace PetCare.Domain.Entities;

public class Supplier : AuditableEntity
{
    public Guid? OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public SupplierStatus Status { get; set; } = SupplierStatus.Active;

    public ICollection<MedicineBatch> Batches { get; set; } = new List<MedicineBatch>();
}