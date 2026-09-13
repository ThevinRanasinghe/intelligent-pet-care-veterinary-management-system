namespace PetCare.Domain.Common;

/// <summary>
/// Base class for entities that track creation/modification timestamps.
/// CreatedAt/UpdatedAt are set by the persistence layer, never directly by client code.
/// </summary>
public abstract class AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
