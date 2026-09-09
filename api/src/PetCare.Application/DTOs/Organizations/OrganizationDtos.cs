using PetCare.Domain.Enums;

namespace PetCare.Application.DTOs.Organizations;

public sealed record OrganizationDetailsDto(
    Guid Id,
    string Name,
    string? RegistrationNumber,
    string Email,
    string Phone,
    string Address,
    string City,
    string Country,
    string Status,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int StaffCount = 0
);

public sealed record CreateOrganizationDto(
    string Name,
    string? RegistrationNumber,
    string Email,
    string Phone,
    string Address,
    string City,
    string Country,
    OrganizationStatus Status = OrganizationStatus.Active
);

public sealed record UpdateOrganizationDto(
    string Name,
    string? RegistrationNumber,
    string Email,
    string Phone,
    string Address,
    string City,
    string Country
);

public sealed record UpdateOrganizationStatusDto(
    OrganizationStatus Status
);
