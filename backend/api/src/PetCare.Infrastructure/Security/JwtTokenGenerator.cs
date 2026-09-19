using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Security;

/// <summary>
/// Issues signed JWTs (HMAC-SHA256) carrying the user id (sub /
/// ClaimTypes.NameIdentifier) and role (ClaimTypes.Role) claims, matching
/// the claims ASP.NET Core's JWT bearer + [Authorize(Roles = ...)]
/// middleware expects out of the box.
/// </summary>
public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtOptions _options;

    public JwtTokenGenerator(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public GeneratedToken GenerateToken(User user)
    {
        if (string.IsNullOrWhiteSpace(_options.Key))
        {
            throw new InvalidOperationException(
                "JWT signing key is not configured. Set it via user secrets: " +
                "dotnet user-secrets set \"Jwt:Key\" \"<a long random secret>\" " +
                "or via the environment variable PETCARE_JWT_KEY.");
        }

        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(_options.ExpiryMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new GeneratedToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
