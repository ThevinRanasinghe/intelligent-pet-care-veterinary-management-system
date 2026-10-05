using PetCare.Domain.Entities;

namespace PetCare.Application.Interfaces;

public record GeneratedToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>
/// Generates signed JWTs carrying the user id ("sub"/NameIdentifier) and
/// role claims. Implemented in PetCare.Infrastructure using
/// System.IdentityModel.Tokens.Jwt; signing configuration (issuer,
/// audience, key, expiry) comes from IConfiguration, sourced from
/// environment variables / user-secrets, never hardcoded.
/// </summary>
public interface IJwtTokenGenerator
{
    GeneratedToken GenerateToken(User user);
}
