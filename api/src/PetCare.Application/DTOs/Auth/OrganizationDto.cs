namespace PetCare.Application.DTOs.Auth;

/// <summary>
/// Safe public summary of a Veterinary Organization.
/// </summary>
public sealed record OrganizationDto(
    Guid Id,
    string Name,
    string Email,
    string? Phone,
    string Status
);
