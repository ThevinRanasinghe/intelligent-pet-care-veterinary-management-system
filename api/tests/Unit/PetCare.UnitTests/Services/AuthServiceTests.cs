using FluentAssertions;
using Moq;
using PetCare.Application.DTOs.Auth;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;
using PetCare.Infrastructure.Entities;
using PetCare.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace PetCare.UnitTests.Services;

/// <summary>
/// Unit tests for AuthService covering registration, login, and role security.
/// </summary>
public sealed class AuthServiceTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<ITokenService>               _tokenServiceMock;
    private readonly AuthService                       _authService;

    public AuthServiceTests()
    {
        // UserManager requires a store mock for construction
        var store = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _tokenServiceMock = new Mock<ITokenService>();
        _authService      = new AuthService(_userManagerMock.Object, _tokenServiceMock.Object);
    }

    // =========================================================================
    // REGISTRATION TESTS
    // =========================================================================

    [Fact]
    public async Task RegisterPetOwner_ValidRequest_ReturnsCurrentUserDto()
    {
        // Arrange
        var request = new RegisterPetOwnerRequestDto(
            "John", "Doe", "john@example.com", "Password1!", "Password1!");

        _userManagerMock
            .Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync((ApplicationUser?)null);

        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Success);

        _userManagerMock
            .Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), Roles.PetOwner))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _authService.RegisterPetOwnerAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Email.Should().Be(request.Email);
        result.Role.Should().Be(Roles.PetOwner);
        result.FirstName.Should().Be("John");
        result.LastName.Should().Be("Doe");
    }

    [Fact]
    public async Task RegisterPetOwner_DuplicateEmail_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = new RegisterPetOwnerRequestDto(
            "Jane", "Doe", "existing@example.com", "Password1!", "Password1!");

        _userManagerMock
            .Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync(new ApplicationUser { Email = request.Email });

        // Act
        var act = () => _authService.RegisterPetOwnerAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task RegisterPetOwner_RoleAlwaysPetOwner_NeverPrivileged()
    {
        // Arrange
        var request = new RegisterPetOwnerRequestDto(
            "Attacker", "Evil", "attacker@example.com", "Password1!", "Password1!");

        _userManagerMock
            .Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync((ApplicationUser?)null);

        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Success);

        _userManagerMock
            .Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), Roles.PetOwner))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _authService.RegisterPetOwnerAsync(request);

        // Assert — role must ALWAYS be PetOwner, never SuperAdmin/ClinicManager/etc.
        result.Role.Should().Be(Roles.PetOwner);
        result.Role.Should().NotBe(Roles.SuperAdmin);
        result.Role.Should().NotBe(Roles.ClinicManager);
        result.Role.Should().NotBe(Roles.InventoryOfficer);
        result.Role.Should().NotBe(Roles.Veterinarian);

        // Verify PetOwner was the role added, never anything else
        _userManagerMock.Verify(
            m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), Roles.PetOwner),
            Times.Once);
        _userManagerMock.Verify(
            m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.Is<string>(r => r != Roles.PetOwner)),
            Times.Never);
    }

    [Fact]
    public async Task RegisterPetOwner_IdentityFails_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = new RegisterPetOwnerRequestDto(
            "Bad", "Password", "bad@example.com", "weak", "weak");

        _userManagerMock
            .Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync((ApplicationUser?)null);

        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Failed(
                new IdentityError { Description = "Password too weak." }));

        // Act
        var act = () => _authService.RegisterPetOwnerAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Password too weak*");
    }

    // =========================================================================
    // LOGIN TESTS
    // =========================================================================

    [Fact]
    public async Task Login_ValidCredentials_ReturnsLoginResponseWithJwt()
    {
        // Arrange
        var request = new LoginRequestDto("user@example.com", "Password1!");
        var user = new ApplicationUser
        {
            Id        = Guid.NewGuid().ToString(),
            Email     = request.Email,
            FirstName = "Test",
            LastName  = "User",
            IsActive  = true,
        };

        _userManagerMock
            .Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);

        _userManagerMock
            .Setup(m => m.CheckPasswordAsync(user, request.Password))
            .ReturnsAsync(true);

        _userManagerMock
            .Setup(m => m.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { Roles.PetOwner });

        var expiry = DateTime.UtcNow.AddHours(1);
        _tokenServiceMock
            .Setup(m => m.GenerateToken(
                user.Id, user.Email!, Roles.PetOwner, user.FirstName, user.LastName))
            .Returns(("jwt.token.here", expiry));

        // Act
        var result = await _authService.LoginAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.User.Email.Should().Be(request.Email);
        result.User.Role.Should().Be(Roles.PetOwner);
    }

    [Fact]
    public async Task Login_UserNotFound_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var request = new LoginRequestDto("notfound@example.com", "Password1!");

        _userManagerMock
            .Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        var act = () => _authService.LoginAsync(request);

        // Assert — generic message that does NOT reveal whether email exists
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid email or password.");
    }

    [Fact]
    public async Task Login_WrongPassword_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var request = new LoginRequestDto("user@example.com", "WrongPassword");
        var user = new ApplicationUser
        {
            Email    = request.Email,
            IsActive = true
        };

        _userManagerMock
            .Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);

        _userManagerMock
            .Setup(m => m.CheckPasswordAsync(user, request.Password))
            .ReturnsAsync(false);

        // Act
        var act = () => _authService.LoginAsync(request);

        // Assert — same generic message for wrong password (doesn't reveal email exists)
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid email or password.");
    }

    [Fact]
    public async Task Login_InactiveUser_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var request = new LoginRequestDto("inactive@example.com", "Password1!");
        var user = new ApplicationUser
        {
            Email    = request.Email,
            IsActive = false  // Deactivated account
        };

        _userManagerMock
            .Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);

        // Act
        var act = () => _authService.LoginAsync(request);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Invalid email or password.");
    }
}
