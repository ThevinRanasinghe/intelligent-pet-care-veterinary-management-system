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
}
