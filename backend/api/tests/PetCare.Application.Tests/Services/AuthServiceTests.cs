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
    private readonly Mock<IOrganizationRepository> _organizationRepository = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IJwtTokenGenerator> _jwtTokenGenerator = new();
    private readonly Mock<IValidator<LoginRequest>> _loginValidator = new();
    private readonly Mock<IValidator<RegisterPetOwnerRequest>> _registerPetOwnerValidator = new();
    private readonly Mock<IValidator<RegisterOrganizationRequest>> _registerOrganizationValidator = new();

    private static readonly Guid UserId = Guid.NewGuid();
    private const string Email = "manager@petcare.lk";
    private const string Password = "ChangeMe123!";
    private const string PasswordHash = "hashed:ChangeMe123!";

    public AuthServiceTests()
    {
        _loginValidator
            .Setup(v => v.ValidateAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _registerPetOwnerValidator
            .Setup(v => v.ValidateAsync(It.IsAny<RegisterPetOwnerRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        _registerOrganizationValidator
            .Setup(v => v.ValidateAsync(It.IsAny<RegisterOrganizationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
    }

    private AuthService CreateService() => new(
        _userRepository.Object,
        _organizationRepository.Object,
        _passwordHasher.Object,
        _jwtTokenGenerator.Object,
        _loginValidator.Object,
        _registerPetOwnerValidator.Object,
        _registerOrganizationValidator.Object);

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

    [Fact]
    public async Task RegisterPetOwnerAsync_WithValidRequest_CreatesUserAndReturnsProfile()
    {
        _userRepository.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _passwordHasher.Setup(h => h.HashPassword(Password)).Returns("hashed-pw");

        var service = CreateService();
        var request = new RegisterPetOwnerRequest
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john@example.com",
            Password = Password,
            ConfirmPassword = Password,
        };

        var response = await service.RegisterPetOwnerAsync(request);

        Assert.Equal("john@example.com", response.Email);
        Assert.Equal(Roles.PetOwner, response.Role);
        Assert.Equal("John Doe", response.FullName);
        _userRepository.Verify(r => r.AddAsync(It.Is<User>(u => u.Role == Roles.PetOwner), It.IsAny<CancellationToken>()), Times.Once);
        _userRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterPetOwnerAsync_WithDuplicateEmail_ThrowsInvalidOperationException()
    {
        _userRepository.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var service = CreateService();
        var request = new RegisterPetOwnerRequest
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "dup@example.com",
            Password = Password,
            ConfirmPassword = Password,
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RegisterPetOwnerAsync(request));
    }

    [Fact]
    public async Task RegisterOrganizationAsync_WithValidRequest_CreatesOrganizationAndManager()
    {
        _userRepository.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _organizationRepository.Setup(r => r.NameOrEmailExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _passwordHasher.Setup(h => h.HashPassword(Password)).Returns("hashed-pw");

        var service = CreateService();
        var request = new RegisterOrganizationRequest
        {
            OrganizationName = "Happy Paws",
            OrganizationEmail = "contact@happypaws.com",
            OrganizationPhone = "+1 555-0199",
            Address = "123 Vet Blvd",
            City = "Metropolis",
            Country = "United States",
            ManagerFirstName = "Jane",
            ManagerLastName = "Smith",
            ManagerEmail = "jane@happypaws.com",
            Password = Password,
            ConfirmPassword = Password,
        };

        var response = await service.RegisterOrganizationAsync(request);

        Assert.Equal("jane@happypaws.com", response.Email);
        Assert.Equal(Roles.ClinicManager, response.Role);
        Assert.NotNull(response.Organization);
        _organizationRepository.Verify(r => r.AddAsync(It.IsAny<Organization>(), It.IsAny<CancellationToken>()), Times.Once);
        _userRepository.Verify(r => r.AddAsync(It.Is<User>(u => u.Role == Roles.ClinicManager), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterOrganizationAsync_WithDuplicateManagerEmail_ThrowsInvalidOperationException()
    {
        _userRepository.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var service = CreateService();
        var request = new RegisterOrganizationRequest
        {
            OrganizationName = "Happy Paws",
            OrganizationEmail = "contact@happypaws.com",
            OrganizationPhone = "+1 555-0199",
            Address = "123 Vet Blvd",
            City = "Metropolis",
            Country = "United States",
            ManagerFirstName = "Jane",
            ManagerLastName = "Smith",
            ManagerEmail = "dup@happypaws.com",
            Password = Password,
            ConfirmPassword = Password,
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RegisterOrganizationAsync(request));
    }

    [Fact]
    public async Task RegisterOrganizationAsync_WithDuplicateOrgName_ThrowsInvalidOperationException()
    {
        _userRepository.Setup(r => r.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _organizationRepository.Setup(r => r.NameOrEmailExistsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var service = CreateService();
        var request = new RegisterOrganizationRequest
        {
            OrganizationName = "Happy Paws",
            OrganizationEmail = "contact@happypaws.com",
            OrganizationPhone = "+1 555-0199",
            Address = "123 Vet Blvd",
            City = "Metropolis",
            Country = "United States",
            ManagerFirstName = "Jane",
            ManagerLastName = "Smith",
            ManagerEmail = "jane@happypaws.com",
            Password = Password,
            ConfirmPassword = Password,
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RegisterOrganizationAsync(request));
    }
}
