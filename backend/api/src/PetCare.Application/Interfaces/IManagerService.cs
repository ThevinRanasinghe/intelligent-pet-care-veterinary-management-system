using PetCare.Application.DTOs.Manager;

namespace PetCare.Application.Interfaces;

/// <summary>
/// ClinicManager operations scoped to the caller's own organization.
/// Staff accounts created here always land inside the authenticated
/// manager's organization — the client never supplies an organization id.
/// </summary>
public interface IManagerService
{
    /// <summary>
    /// Creates a Veterinarian account inside the authenticated
    /// ClinicManager's organization. Role and OrganizationId are assigned
    /// server-side; the account starts Active with MustChangePassword =
    /// true and a one-time temporary password.
    /// </summary>
    Task<CreatedStaffAccountResponse> CreateVeterinarianAsync(
        ManagerCreateStaffRequest request, CancellationToken cancellationToken = default);

    /// <summary>Same as <see cref="CreateVeterinarianAsync"/> but for InventoryOfficer.</summary>
    Task<CreatedStaffAccountResponse> CreateInventoryOfficerAsync(
        ManagerCreateStaffRequest request, CancellationToken cancellationToken = default);

    /// <summary>Active veterinarians in the caller's organization.</summary>
    Task<IReadOnlyList<ManagerVeterinarianResponse>> GetVeterinariansAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Work history for a veterinarian in the caller's organization:
    /// appointment counts, examinations, prescriptions/medicine requests,
    /// and bills. Optionally bounded by a date range.
    /// </summary>
    Task<VeterinarianHistoryResponse> GetVeterinarianHistoryAsync(
        Guid veterinarianId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default);
}
