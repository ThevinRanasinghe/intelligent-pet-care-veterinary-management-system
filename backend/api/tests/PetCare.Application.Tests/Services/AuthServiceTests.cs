using FluentValidation;
using FluentValidation.Results;
using Moq;
using PetCare.Application.DTOs.Auth;
using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;
using PetCare.Application.Services;
using PetCare.Domain.Constants;
using PetCare.Domain.Entities;
using Xunit;

namespace PetCare.Application.Tests.Services;

/// <summary>
/// Unit tests for AuthService: successful login issues a token and never
/// leaks the password hash; unknown email, wrong password and inactive
/// accounts all fail the same way (InvalidCredentialsException -> 401),
/// to avoid leaking account existence. All dependencies (repository,
/// password hasher, token generator, validator) are mocked.
/// </summary>
public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IJwtTokenGenerator> _jwtTokenGenerator = new();
    private readonly Mock<IValidator<LoginRequest>> _loginValidator = new();

    private static readonly Guid UserId = Guid.NewGuid();
    private const string Email = "manager@petcare.lk";
    private const string Password = "ChangeMe123!";
    private const string PasswordHash = "hashed:ChangeMe123!";

    public AuthServiceTests()
    {
        _loginValidator
            .Setup(v => v.ValidateAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
    }

    private AuthService CreateService() => new(
        _userRepository.Object,
        _passwordHasher.Object,
        _jwtTokenGenerator.Object,
        _loginValidator.Object);

    private static User ActiveManager() => new()
    {
        Id = UserId,
        Email = Email,
        PasswordHash = PasswordHash,
        Name = "Miran Perera",
        Role = Roles.ClinicManager,
        Active = true
    };

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsTokenAndProfile()
    {
        var user = ActiveManager();
        _userRepository.Setup(r => r.GetByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _passwordHasher.Setup(h => h.VerifyPassword(Password, PasswordHash)).Returns(true);

        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
        _jwtTokenGenerator.Setup(g => g.GenerateToken(user)).Returns(new GeneratedToken("signed-jwt", expiresAt));

        var service = CreateService();
        var response = await service.LoginAsync(new LoginRequest { Email = Email, Password = Password });

        Assert.Equal("signed-jwt", response.Token);
        Assert.Equal(expiresAt, response.ExpiresAt);
        Assert.Equal(UserId, response.UserId);
        Assert.Equal(Email, response.Email);
        Assert.Equal("Miran Perera", response.Name);
        Assert.Equal(Roles.ClinicManager, response.Role);
    }

    [Fact]
    public async Task LoginAsync_WithUnknownEmail_ThrowsInvalidCredentials()
    {
        _userRepository.Setup(r => r.GetByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        var service = CreateService();

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => service.LoginAsync(new LoginRequest { Email = Email, Password = Password }));

        _jwtTokenGenerator.Verify(g => g.GenerateToken(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ThrowsInvalidCredentials()
    {
        var user = ActiveManager();
        _userRepository.Setup(r => r.GetByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _passwordHasher.Setup(h => h.VerifyPassword(Password, PasswordHash)).Returns(false);

        var service = CreateService();

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => service.LoginAsync(new LoginRequest { Email = Email, Password = Password }));

        _jwtTokenGenerator.Verify(g => g.GenerateToken(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WithInactiveAccount_ThrowsInvalidCredentials()
    {
        var user = ActiveManager();
        user.Active = false;
        _userRepository.Setup(r => r.GetByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _passwordHasher.Setup(h => h.VerifyPassword(Password, PasswordHash)).Returns(true);

        var service = CreateService();

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => service.LoginAsync(new LoginRequest { Email = Email, Password = Password }));

        _jwtTokenGenerator.Verify(g => g.GenerateToken(It.IsAny<User>()), Times.Never);
    }
}
