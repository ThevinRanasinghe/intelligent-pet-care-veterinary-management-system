namespace PetCare.Application.Interfaces;

/// <summary>
/// Application contract for JWT token generation.
/// Uses primitive types only — no entity references — keeping Application
/// independent of Infrastructure.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Generates a signed JWT for the given user details and role.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <param name="email">The user's email address.</param>
    /// <param name="role">The user's application role.</param>
    /// <param name="firstName">The user's first name (included as a claim).</param>
    /// <param name="lastName">The user's last name (included as a claim).</param>
    /// <returns>A tuple of the token string and its expiry time (UTC).</returns>
    (string Token, DateTime ExpiresAt) GenerateToken(
        string userId,
        string email,
        string role,
        string firstName,
        string lastName);
}
