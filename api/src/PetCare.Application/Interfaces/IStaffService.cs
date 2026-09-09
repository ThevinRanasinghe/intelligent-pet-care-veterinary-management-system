using PetCare.Application.DTOs.Users;

namespace PetCare.Application.Interfaces;

/// <summary>
/// Service contract for ClinicManager staff account management.
/// Guaranteed organization-level multi-tenant data isolation:
/// all actions operate strictly on the current authenticated ClinicManager's organization.
/// </summary>
public interface IStaffService
{
    Task<IEnumerable<StaffUserDto>> GetOrganizationStaffAsync(
        string? search,
        string? role,
        string? status,
        CancellationToken ct = default);

    Task<StaffUserDto> GetStaffMemberByIdAsync(
        string staffId,
        CancellationToken ct = default);

    Task<StaffUserDto> CreateStaffMemberAsync(
        CreateStaffUserRequestDto request,
        CancellationToken ct = default);

    Task<StaffUserDto> UpdateStaffMemberAsync(
        string staffId,
        UpdateStaffUserRequestDto request,
        CancellationToken ct = default);

    Task<StaffUserDto> UpdateStaffStatusAsync(
        string staffId,
        UpdateStaffStatusRequestDto request,
        CancellationToken ct = default);
}
