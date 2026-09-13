namespace PetCare.Domain.Common;

/// <summary>
/// Base class for entities that track creation/modification timestamps.
/// CreatedAt/UpdatedAt are set by the persistence layer (e.g. a SaveChanges
/// interceptor in PetCare.Infrastructure), never directly by client code.
/// </summary>
public abstract class AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
