namespace PetCare.Application.DTOs.Auth;

/// <summary>
/// Never includes the password or password hash. The JWT itself carries the
/// user id and role as claims (see IJwtTokenGenerator); UserId/Role are also
/// duplicated here so the frontend can render role-aware UI without
/// decoding the token.
/// </summary>
public class LoginResponse
{
    public string Token { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public Guid UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;
}
