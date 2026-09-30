using PetCare.Domain.Entities;

namespace PetCare.Application.Interfaces;

/// <summary>
/// Data-access abstraction for Examination. Implemented in
/// PetCare.Infrastructure using EF Core; the Application layer never
/// references EF Core directly. Organization scoping applies transitively
/// through the attending veterinarian (Veterinarian.OrganizationId).
/// </summary>
public interface IExaminationRepository
{
    /// <summary>
    /// Loads an examination with the graph needed for billing: Veterinarian,
    /// Pet/Owner, and Diagnosis -&gt; TreatmentRecords -&gt; Prescriptions
    /// -&gt; Medicine.
    /// </summary>
    Task<Examination?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>All examinations attended by the given veterinarian.</summary>
    Task<IReadOnlyList<Examination>> GetByVeterinarianAsync(Guid veterinarianId, CancellationToken cancellationToken = default);
}
