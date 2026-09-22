namespace PetCare.Application.Interfaces;

/// <summary>
/// Resolves the authenticated caller's PetOwner profile (matched by the JWT
/// email claim) and answers "does this caller own this resource?" for the
/// pet/consultation/clinical object graph.
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

    /// <summary>
    /// The PetOwner.Id linked to the current caller (by email). Null for
    /// staff callers and for PetOwner accounts with no owner profile.
    /// </summary>
    Task<string?> GetOwnerIdAsync(CancellationToken cancellationToken = default);

    Task<bool> OwnsPetAsync(string petId, CancellationToken cancellationToken = default);

    Task<bool> OwnsConsultationAsync(string consultationId, CancellationToken cancellationToken = default);

    Task<bool> OwnsExaminationAsync(Guid examinationId, CancellationToken cancellationToken = default);

    Task<bool> OwnsDiagnosisAsync(Guid diagnosisId, CancellationToken cancellationToken = default);

    Task<bool> OwnsTreatmentRecordAsync(Guid treatmentRecordId, CancellationToken cancellationToken = default);

    Task<bool> OwnsPrescriptionAsync(Guid prescriptionId, CancellationToken cancellationToken = default);
}
