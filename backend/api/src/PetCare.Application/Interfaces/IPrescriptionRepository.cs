using PetCare.Domain.Entities;
using PetCare.Domain.Enums;

namespace PetCare.Application.Interfaces;

/// <summary>
/// Data-access abstraction for Prescription used by the medicine-request
/// workflow. Implemented in PetCare.Infrastructure using EF Core.
/// Organization scoping applies transitively through the clinical graph
/// (Prescription -&gt; TreatmentRecord -&gt; Diagnosis -&gt; Examination -&gt;
/// Veterinarian -&gt; Organization).
/// </summary>
public interface IPrescriptionRepository
{
    /// <summary>
    /// Returns the tracked prescription (with TreatmentRecord -&gt; Diagnosis
    /// -&gt; Examination and Medicine loaded) for workflow updates.
    /// </summary>
    Task<Prescription?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Medicine requests in the caller's organization, optionally filtered
    /// by request status, newest first.
    /// </summary>
    Task<IReadOnlyList<Prescription>> GetRequestsAsync(
        MedicineRequestStatus? status,
        CancellationToken cancellationToken = default);

    /// <summary>All prescriptions written on the given veterinarian's examinations.</summary>
    Task<IReadOnlyList<Prescription>> GetByVeterinarianAsync(Guid veterinarianId, CancellationToken cancellationToken = default);
}
