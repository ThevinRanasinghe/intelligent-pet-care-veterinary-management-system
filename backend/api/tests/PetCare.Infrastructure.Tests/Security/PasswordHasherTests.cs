using PetCare.Infrastructure.Security;
using Xunit;

namespace PetCare.Infrastructure.Tests.Security;

/// <summary>
/// Unit tests for the PBKDF2-based PasswordHasher: correct passwords
/// verify, wrong passwords and tampered/malformed hashes are rejected,
/// and two hashes of the same password never collide (random salt).
/// </summary>
public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void HashPassword_ThenVerifyPassword_WithCorrectPassword_ReturnsTrue()
    {
        var hash = _hasher.HashPassword("ChangeMe123!");

        Assert.True(_hasher.VerifyPassword("ChangeMe123!", hash));
    }

    [Fact]
    public void VerifyPassword_WithWrongPassword_ReturnsFalse()
    {
        var hash = _hasher.HashPassword("ChangeMe123!");

        Assert.False(_hasher.VerifyPassword("WrongPassword!", hash));
    }

    [Fact]
    public void HashPassword_CalledTwiceForSamePassword_ProducesDifferentHashes()
    {
        var hash1 = _hasher.HashPassword("ChangeMe123!");
        var hash2 = _hasher.HashPassword("ChangeMe123!");

        Assert.NotEqual(hash1, hash2);
    }

    [Theory]
    [InlineData("not-a-valid-hash")]
    [InlineData("100000.not-base64.also-not-base64")]
    [InlineData("")]
    public void VerifyPassword_WithMalformedHash_ReturnsFalse(string malformedHash)
    {
        Assert.False(_hasher.VerifyPassword("ChangeMe123!", malformedHash));
    }
}
