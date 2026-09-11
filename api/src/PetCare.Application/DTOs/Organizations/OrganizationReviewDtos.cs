namespace PetCare.Application.DTOs.Organizations;

/// <summary>
/// Request to reject a pending veterinary organization registration.
/// </summary>
public sealed record RejectOrganizationDto(string Reason);

/// <summary>
/// Request to suspend an active organization.
/// </summary>
public sealed record SuspendOrganizationDto(string? Reason);
