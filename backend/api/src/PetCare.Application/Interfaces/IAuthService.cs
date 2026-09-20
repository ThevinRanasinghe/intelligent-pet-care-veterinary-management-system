using PetCare.Application.DTOs.Auth;

namespace PetCare.Application.Interfaces;

/// <summary>
/// Authentication use cases. See docs/api for the planned /api/auth/login
/// endpoint (added by PetCare.Api.Controllers.AuthController).
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Validates credentials and returns a signed JWT plus basic profile
    /// info. Throws <see cref="PetCare.Application.Exceptions.InvalidCredentialsException"/>
    /// when the email is unknown, the account is inactive, or the password
    /// does not match.
    /// </summary>
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a new PetOwner account. Role is always PetOwner —
    /// never user-supplied. Throws <see cref="InvalidOperationException"/>
    /// if an account with the email already exists.
    /// </summary>
    Task<CurrentUserResponse> RegisterPetOwnerAsync(RegisterPetOwnerRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a new veterinary organization and its initial
    /// ClinicManager administrator. Role is always ClinicManager — never
    /// user-supplied. The organization starts in Pending status and must
    /// be verified by a SuperAdmin before staff can log in. Throws
    /// <see cref="InvalidOperationException"/> for duplicate
    /// organization name/email or duplicate manager email.
    /// </summary>
    Task<CurrentUserResponse> RegisterOrganizationAsync(RegisterOrganizationRequest request, CancellationToken cancellationToken = default);
}
