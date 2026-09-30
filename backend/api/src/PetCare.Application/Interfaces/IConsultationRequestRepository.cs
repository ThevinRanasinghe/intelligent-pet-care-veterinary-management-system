using PetCare.Domain.Entities;

namespace PetCare.Application.Interfaces;

/// <summary>
/// Data-access abstraction for ConsultationRequest. Implemented in
/// PetCare.Infrastructure using EF Core; the Application layer never
/// references EF Core directly.
/// </summary>
public interface IConsultationRequestRepository
{
    /// <summary>
    /// Returns the tracked entity (no AsNoTracking) so workflow updates —
    /// status transitions and appended status-history rows — persist on the
    /// next SaveChanges. Reads are organization-scoped for staff callers:
    /// a foreign-organization request resolves to null.
    /// </summary>
    Task<ConsultationRequest?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task AddAsync(ConsultationRequest request, CancellationToken cancellationToken = default);
}
