using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using PetCare.Domain.Constants;
using PetCare.Domain.Entities;
using PetCare.Infrastructure.Security;
using Xunit;

namespace PetCare.Infrastructure.Tests.Security;

/// <summary>
/// Unit tests for JwtTokenGenerator: issued tokens carry the user id and
/// role as claims (so ASP.NET Core's [Authorize(Roles = ...)] middleware
/// can read them), respect the configured expiry, and generation fails
/// fast when no signing key is configured.
/// </summary>
public class JwtTokenGeneratorTests
{
    private static readonly User Manager = new()
    {
        Id = Guid.NewGuid(),
        Email = "manager@petcare.lk",
        Name = "Miran Perera",
        Role = Roles.ClinicManager,
        Active = true
    };

    private static JwtTokenGenerator CreateGenerator(JwtOptions options) =>
        new(Options.Create(options));

    [Fact]
    public void GenerateToken_IncludesUserIdAndRoleClaims()
    {
        var generator = CreateGenerator(new JwtOptions
        {
            Issuer = "PetCareApi",
            Audience = "PetCareClient",
            ExpiryMinutes = 60,
            Key = "unit-test-signing-key-at-least-32-bytes-long"
        });

        var result = generator.GenerateToken(Manager);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);
        Assert.Equal(Manager.Id.ToString(), token.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal(Roles.ClinicManager, token.Claims.First(c => c.Type == ClaimTypes.Role).Value);
        Assert.Equal(Manager.Email, token.Claims.First(c => c.Type == ClaimTypes.Email).Value);
    }

    [Fact]
    public void GenerateToken_SetsExpiryBasedOnConfiguredMinutes()
    {
        var generator = CreateGenerator(new JwtOptions
        {
            Key = "unit-test-signing-key-at-least-32-bytes-long",
            ExpiryMinutes = 15
        });

        var before = DateTimeOffset.UtcNow;
        var result = generator.GenerateToken(Manager);

        Assert.True(result.ExpiresAt > before.AddMinutes(14));
        Assert.True(result.ExpiresAt < before.AddMinutes(16));
    }

    [Fact]
    public void GenerateToken_WithoutConfiguredKey_Throws()
    {
        var generator = CreateGenerator(new JwtOptions { Key = "" });

        Assert.Throws<InvalidOperationException>(() => generator.GenerateToken(Manager));
    }
}
