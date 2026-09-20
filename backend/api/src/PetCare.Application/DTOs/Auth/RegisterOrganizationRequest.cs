namespace PetCare.Application.DTOs.Auth;

/// <summary>
/// Public Veterinary Organization registration request. Creates both the
/// Organization and its initial ClinicManager administrator account. Role
/// is ALWAYS ClinicManager — cannot be chosen by the user.
/// </summary>
public sealed class RegisterOrganizationRequest
{
    // ── Organization Details ──
    public string OrganizationName { get; set; } = string.Empty;

    public string? RegistrationNumber { get; set; }

    public string OrganizationEmail { get; set; } = string.Empty;

    public string OrganizationPhone { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    // ── Primary Clinic Manager Account ──
    public string ManagerFirstName { get; set; } = string.Empty;

    public string ManagerLastName { get; set; } = string.Empty;

    public string ManagerEmail { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string ConfirmPassword { get; set; } = string.Empty;
}
