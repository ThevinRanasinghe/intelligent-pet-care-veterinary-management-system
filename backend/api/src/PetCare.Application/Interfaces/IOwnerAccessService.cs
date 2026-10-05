namespace PetCare.Application.Interfaces;

/// <summary>
/// Resolves the authenticated caller's PetOwner profile through the
/// PetOwner.UserId foreign key (JWT sub/NameIdentifier claim) and answers
/// "does this caller own this resource?" for the pet/consultation/clinical
/// object graph.
///
/// Staff roles are never restricted by these checks — callers must gate on
/// <see cref="IsPetOwner"/> first.
/// </summary>
public interface IOwnerAccessService
{
    /// <summary>True when the current caller has the PetOwner role.</summary>
    bool IsPetOwner { get; }

    /// <summary>The authenticated caller's email claim, if present.</summary>
    string? CallerEmail { get; }

    /// <summary>The authenticated caller's user id (JWT sub claim), if present.</summary>
    Guid? CallerUserId { get; }

    /// <summary>
    /// The PetOwner.Id linked to the current caller via PetOwner.UserId.
    /// Null for staff callers and for PetOwner accounts with no linked
    /// owner profile.
    /// </summary>
    Task<string?> GetOwnerIdAsync(CancellationToken cancellationToken = default);

    Task<bool> OwnsPetAsync(string petId, CancellationToken cancellationToken = default);

    Task<bool> OwnsConsultationAsync(string consultationId, CancellationToken cancellationToken = default);

    Task<bool> OwnsExaminationAsync(Guid examinationId, CancellationToken cancellationToken = default);

    Task<bool> OwnsDiagnosisAsync(Guid diagnosisId, CancellationToken cancellationToken = default);

    Task<bool> OwnsTreatmentRecordAsync(Guid treatmentRecordId, CancellationToken cancellationToken = default);

    Task<bool> OwnsPrescriptionAsync(Guid prescriptionId, CancellationToken cancellationToken = default);
}
