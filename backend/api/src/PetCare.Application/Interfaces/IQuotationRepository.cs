using PetCare.Domain.Entities;

namespace PetCare.Application.Interfaces;

/// <summary>
/// Data-access abstraction for Quotation (with its line items). Implemented
/// in PetCare.Infrastructure using EF Core; the Application layer never
/// references EF Core directly.
/// </summary>
public interface IQuotationRepository
{
    /// <summary>
    /// Loads a quotation together with its line items.
    /// </summary>
    Task<Quotation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Quotation>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Used to enforce the 1:1 Appointment&lt;-&gt;Quotation relationship before
    /// creating a new quotation.
    /// </summary>
    Task<bool> ExistsForAppointmentAsync(Guid appointmentId, CancellationToken cancellationToken = default);

    /// <summary>The quotation (with line items) for the given appointment.</summary>
    Task<Quotation?> GetByAppointmentIdAsync(Guid appointmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// All quotations for appointments of pets owned by the given owner id.
    /// Deliberately NOT organization-scoped — PetOwner callers have no
    /// organization.
    /// </summary>
    Task<IReadOnlyList<Quotation>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken = default);

    /// <summary>All quotations for appointments attended by the given veterinarian.</summary>
    Task<IReadOnlyList<Quotation>> GetByVeterinarianAsync(Guid veterinarianId, CancellationToken cancellationToken = default);

    Task AddAsync(Quotation quotation, CancellationToken cancellationToken = default);
}
