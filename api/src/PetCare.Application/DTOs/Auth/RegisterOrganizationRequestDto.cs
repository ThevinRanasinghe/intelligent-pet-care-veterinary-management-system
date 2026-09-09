namespace PetCare.Application.DTOs.Auth;

/// <summary>
/// Public Veterinary Organization registration request.
/// Creates both the Organization and its initial ClinicManager administrator account.
/// Role is ALWAYS ClinicManager — cannot be chosen by the user.
/// </summary>
public sealed record RegisterOrganizationRequestDto(
    // ── Organization Details ──
    string OrganizationName,
    string? RegistrationNumber,
    string OrganizationEmail,
    string OrganizationPhone,
    string Address,
    string City,
    string Country,

    // ── Primary Clinic Manager Account ──
    string ManagerFirstName,
    string ManagerLastName,
    string ManagerEmail,
    string Password,
    string ConfirmPassword
);
