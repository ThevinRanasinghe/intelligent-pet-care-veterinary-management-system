using PetCare.Domain.Entities;

namespace PetCare.Application.Interfaces;

/// <summary>
/// Data-access abstraction for Veterinarian. Implemented in
/// PetCare.Infrastructure using EF Core; the Application layer never
/// references EF Core directly.
/// </summary>
public interface IVeterinarianRepository
{
    Task<Veterinarian?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
