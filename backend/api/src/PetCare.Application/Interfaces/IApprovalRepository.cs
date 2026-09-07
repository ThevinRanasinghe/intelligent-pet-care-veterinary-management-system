using PetCare.Domain.Entities;

namespace PetCare.Application.Interfaces;

/// <summary>
/// Data-access abstraction for Approval (with its Quotation and History).
/// Implemented in PetCare.Infrastructure using EF Core; the Application
/// layer never references EF Core directly.
/// </summary>
public interface IApprovalRepository
{
    /// <summary>
    /// Loads an approval together with its linked Quotation (needed to
    /// re-validate/update Quotation.Status alongside the approval decision)
    /// and its History.
    /// </summary>
    Task<Approval?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the current approval record for a quotation, if one exists.
    /// Used to enforce the 1:1 Quotation&lt;-&gt;Approval relationship.
    /// </summary>
    Task<Approval?> GetByQuotationIdAsync(Guid quotationId, CancellationToken cancellationToken = default);

    /// <summary>Approvals currently awaiting a Clinic Manager decision.</summary>
    Task<IReadOnlyList<Approval>> GetPendingAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Approval approval, CancellationToken cancellationToken = default);

    Task AddHistoryAsync(ApprovalHistory history, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApprovalHistory>> GetHistoryAsync(Guid approvalId, CancellationToken cancellationToken = default);
}
