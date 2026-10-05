namespace PetCare.Application.DTOs.Auth;

/// <summary>
/// Safe public representation of the registered (or current) user. Never
/// includes PasswordHash or any internal field.
/// </summary>
public sealed class CurrentUserResponse
{
    public Guid Id { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public bool MustChangePassword { get; set; }

    public OrganizationDto? Organization { get; set; }
}

public sealed class OrganizationDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";
}
