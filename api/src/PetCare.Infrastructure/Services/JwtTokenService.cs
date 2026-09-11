using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PetCare.Application.Interfaces;

namespace PetCare.Infrastructure.Services;

/// <summary>
/// Generates signed JWT tokens using configuration from Jwt:Key, Jwt:Issuer,
/// Jwt:Audience, and Jwt:ExpiryMinutes.
/// The signing key is NEVER hardcoded — it is read from configuration/User Secrets.
/// </summary>
public sealed class JwtTokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string Token, DateTime ExpiresAt) GenerateToken(
        string userId,
        string email,
        string role,
        string firstName,
        string lastName,
        Guid? organizationId = null)
    {
        var jwtKey = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key configuration is missing.");

        var issuer   = _configuration["Jwt:Issuer"]   ?? "PetCareAI";
        var audience = _configuration["Jwt:Audience"] ?? "PetCareAIClients";

        if (!int.TryParse(_configuration["Jwt:ExpiryMinutes"], out var expiryMinutes))
            expiryMinutes = 60;

        var keyBytes    = Encoding.UTF8.GetBytes(jwtKey);
        var securityKey = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub,   userId),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role,               role),
            new Claim("firstName",                   firstName),
            new Claim("lastName",                    lastName),
        };

        if (organizationId.HasValue)
        {
            claims.Add(new Claim("organizationId", organizationId.Value.ToString()));
        }

        var token = new JwtSecurityToken(
            issuer:             issuer,
            audience:           audience,
            claims:             claims,
            notBefore:          DateTime.UtcNow,
            expires:            expiresAt,
            signingCredentials: credentials
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
