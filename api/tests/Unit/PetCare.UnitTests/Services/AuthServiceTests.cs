using FluentAssertions;
using Moq;
using PetCare.Application.DTOs.Auth;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;
using PetCare.Infrastructure.Entities;
using PetCare.Infrastructure.Persistence;
using PetCare.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace PetCare.UnitTests.Services;

/// <summary>
/// Unit tests for AuthService covering registration, login, and role security.
/// </summary>
public sealed class AuthServiceTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<ITokenService>               _tokenServiceMock;
    private readonly PetCareDbContext                  _dbContext;
    private readonly AuthService                       _authService;

    public AuthServiceTests()
    {
        // UserManager requires a store mock for construction
        var store = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _tokenServiceMock = new Mock<ITokenService>();

        var options = new DbContextOptionsBuilder<PetCareDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new PetCareDbContext(options);

        _authService = new AuthService(_userManagerMock.Object, _tokenServiceMock.Object, _dbContext);
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
                user.Id, user.Email!, Roles.PetOwner, user.FirstName, user.LastName, It.IsAny<Guid?>()))
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

    // =========================================================================
    // MULTI-ORGANIZATION TESTS
    // =========================================================================

    [Fact]
    public async Task RegisterOrganization_ValidRequest_CreatesOrgAndClinicManager()
    {
        // Arrange
        var request = new RegisterOrganizationRequestDto(
            OrganizationName:   "Happy Paws Clinic",
            RegistrationNumber: "VET-12345",
            OrganizationEmail:  "contact@happypaws.com",
            OrganizationPhone:  "+1234567890",
            Address:            "123 Main St",
            City:               "Metropolis",
            Country:            "USA",
            ManagerFirstName:   "Alice",
            ManagerLastName:    "Smith",
            ManagerEmail:       "alice@happypaws.com",
            Password:           "Password1!",
            ConfirmPassword:    "Password1!"
        );

        _userManagerMock
            .Setup(m => m.FindByEmailAsync(request.ManagerEmail))
            .ReturnsAsync((ApplicationUser?)null);

        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Success);

        _userManagerMock
            .Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), Roles.ClinicManager))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _authService.RegisterOrganizationAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Email.Should().Be(request.ManagerEmail);
        result.Role.Should().Be(Roles.ClinicManager);
        result.Organization.Should().NotBeNull();
        result.Organization!.Name.Should().Be("Happy Paws Clinic");

        // Verify ClinicManager was assigned
        _userManagerMock.Verify(
            m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), Roles.ClinicManager),
            Times.Once);

        // Verify organization was saved in DbContext
        var savedOrg = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Name == "Happy Paws Clinic");
        savedOrg.Should().NotBeNull();
        savedOrg!.Email.Should().Be("contact@happypaws.com");
    }

    [Fact]
    public async Task RegisterOrganization_DuplicateManagerEmail_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = new RegisterOrganizationRequestDto(
            OrganizationName:   "Duplicate Manager Clinic",
            RegistrationNumber: null,
            OrganizationEmail:  "info@dupclinic.com",
            OrganizationPhone:  "555-0100",
            Address:            "456 Oak St",
            City:               "Metropolis",
            Country:            "USA",
            ManagerFirstName:   "Bob",
            ManagerLastName:    "Jones",
            ManagerEmail:       "existing_manager@example.com",
            Password:           "Password1!",
            ConfirmPassword:    "Password1!"
        );

        _userManagerMock
            .Setup(m => m.FindByEmailAsync(request.ManagerEmail))
            .ReturnsAsync(new ApplicationUser { Email = request.ManagerEmail });

        // Act
        var act = () => _authService.RegisterOrganizationAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task Login_OrganizationStaff_IncludesOrgClaimAndOrganizationDto()
    {
        // Arrange
        var org = new Organization
        {
            Id        = Guid.NewGuid(),
            Name      = "City Vet Hospital",
            Email     = "contact@cityvet.com",
            Phone     = "555-0200",
            Address   = "789 Pine Rd",
            City      = "Gotham",
            Country   = "USA",
            Status    = OrganizationStatus.Active,
            IsActive  = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Organizations.Add(org);
        await _dbContext.SaveChangesAsync();

        var request = new LoginRequestDto("vet@cityvet.com", "Password1!");
        var user = new ApplicationUser
        {
            Id             = Guid.NewGuid().ToString(),
            Email          = request.Email,
            FirstName      = "David",
            LastName       = "Miller",
            OrganizationId = org.Id,
            IsActive       = true
        };

        _userManagerMock
            .Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);

        _userManagerMock
            .Setup(m => m.CheckPasswordAsync(user, request.Password))
            .ReturnsAsync(true);

        _userManagerMock
            .Setup(m => m.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { Roles.Veterinarian });

        var expiry = DateTime.UtcNow.AddHours(1);
        _tokenServiceMock
            .Setup(m => m.GenerateToken(
                user.Id, user.Email!, Roles.Veterinarian, user.FirstName, user.LastName, org.Id))
            .Returns(("jwt.token.with.org", expiry));

        // Act
        var result = await _authService.LoginAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.User.Role.Should().Be(Roles.Veterinarian);
        result.User.Organization.Should().NotBeNull();
        result.User.Organization!.Id.Should().Be(org.Id);
        result.User.Organization.Name.Should().Be("City Vet Hospital");

        // Verify token service was invoked with org.Id
        _tokenServiceMock.Verify(
            m => m.GenerateToken(user.Id, user.Email!, Roles.Veterinarian, user.FirstName, user.LastName, org.Id),
            Times.Once);
    }

    [Fact]
    public async Task Login_SuspendedOrganization_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var org = new Organization
        {
            Id        = Guid.NewGuid(),
            Name      = "Suspended Vet Care",
            Email     = "contact@suspendedvet.com",
            Phone     = "555-0300",
            Address   = "101 Elm St",
            City      = "Star City",
            Country   = "USA",
            Status    = OrganizationStatus.Suspended,
            IsActive  = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Organizations.Add(org);
        await _dbContext.SaveChangesAsync();

        var request = new LoginRequestDto("manager@suspendedvet.com", "Password1!");
        var user = new ApplicationUser
        {
            Id             = Guid.NewGuid().ToString(),
            Email          = request.Email,
            FirstName      = "Sam",
            LastName       = "Oak",
            OrganizationId = org.Id,
            IsActive       = true
        };

        _userManagerMock
            .Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);

        _userManagerMock
            .Setup(m => m.CheckPasswordAsync(user, request.Password))
            .ReturnsAsync(true);

        _userManagerMock
            .Setup(m => m.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { Roles.ClinicManager });

        // Act
        var act = () => _authService.LoginAsync(request);

        // Assert — blocked with suspended message
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*suspended*");
    }

    [Fact]
    public async Task Login_PendingAccountStatus_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var request = new LoginRequestDto("pending@vetclinic.com", "Password1!");
        var user = new ApplicationUser
        {
            Id            = Guid.NewGuid().ToString(),
            Email         = request.Email,
            FirstName     = "Pending",
            LastName      = "Manager",
            AccountStatus = UserAccountStatus.Pending,
            IsActive      = true
        };

        _userManagerMock
            .Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);

        _userManagerMock
            .Setup(m => m.CheckPasswordAsync(user, request.Password))
            .ReturnsAsync(true);

        // Act
        var act = () => _authService.LoginAsync(request);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*pending administrator verification*");
    }

    [Fact]
    public async Task Login_RejectedOrganization_ThrowsUnauthorizedAccessExceptionWithReason()
    {
        // Arrange
        var org = new Organization
        {
            Id              = Guid.NewGuid(),
            Name            = "Rejected Vet Clinic",
            Email           = "contact@rejectedvet.com",
            Phone           = "555-0400",
            Address         = "202 Elm St",
            City            = "Star City",
            Country         = "USA",
            Status          = OrganizationStatus.Rejected,
            IsActive        = false,
            RejectionReason = "Invalid veterinary license document.",
            CreatedAt       = DateTime.UtcNow,
            UpdatedAt       = DateTime.UtcNow
        };
        _dbContext.Organizations.Add(org);
        await _dbContext.SaveChangesAsync();

        var request = new LoginRequestDto("manager@rejectedvet.com", "Password1!");
        var user = new ApplicationUser
        {
            Id             = Guid.NewGuid().ToString(),
            Email          = request.Email,
            FirstName      = "Bob",
            LastName       = "Denied",
            OrganizationId = org.Id,
            AccountStatus  = UserAccountStatus.Active,
            IsActive       = true
        };

        _userManagerMock
            .Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);

        _userManagerMock
            .Setup(m => m.CheckPasswordAsync(user, request.Password))
            .ReturnsAsync(true);

        _userManagerMock
            .Setup(m => m.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { Roles.ClinicManager });

        // Act
        var act = () => _authService.LoginAsync(request);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*rejected*Invalid veterinary license*");
    }

    [Fact]
    public async Task Login_DisabledStaffAccount_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var request = new LoginRequestDto("disabled@vetclinic.com", "Password1!");
        var user = new ApplicationUser
        {
            Id            = Guid.NewGuid().ToString(),
            Email         = request.Email,
            FirstName     = "Disabled",
            LastName      = "Staff",
            AccountStatus = UserAccountStatus.Disabled,
            IsActive      = false
        };

        _userManagerMock
            .Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);

        _userManagerMock
            .Setup(m => m.CheckPasswordAsync(user, request.Password))
            .ReturnsAsync(true);

        // Act
        var act = () => _authService.LoginAsync(request);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*disabled*");
    }
}

