using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using PetCare.Application.DTOs.Users;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;
using PetCare.Infrastructure.Entities;
using PetCare.Infrastructure.Persistence;
using PetCare.Infrastructure.Services;
using Xunit;

namespace PetCare.UnitTests.Services;

public sealed class StaffServiceTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<ICurrentUserService>         _currentUserMock;
    private readonly PetCareDbContext                  _dbContext;
    private readonly StaffService                      _staffService;
    private readonly Guid                              _testOrgId = Guid.NewGuid();

    public StaffServiceTests()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _currentUserMock = new Mock<ICurrentUserService>();
        _currentUserMock.Setup(c => c.OrganizationId).Returns(_testOrgId);
        _currentUserMock.Setup(c => c.UserId).Returns("manager-user-id");

        var options = new DbContextOptionsBuilder<PetCareDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new PetCareDbContext(options);

        // Seed organization
        _dbContext.Organizations.Add(new Organization
        {
            Id        = _testOrgId,
            Name      = "Beacon Clinic A",
            Email     = "clinicA@beacon.com",
            Phone     = "555-0100",
            Address   = "123 Main St",
            City      = "Springfield",
            Country   = "USA",
            Status    = OrganizationStatus.Active,
            IsActive  = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        _dbContext.SaveChanges();

        _staffService = new StaffService(_userManagerMock.Object, _dbContext, _currentUserMock.Object);
    }

    [Fact]
    public async Task GetOrganizationStaff_ReturnsOnlyOwnOrganizationStaff()
    {
        // Arrange
        var otherOrgId = Guid.NewGuid();

        var staff1 = new ApplicationUser
        {
            Id             = "user-1",
            Email          = "vet1@clinicA.com",
            FirstName      = "Alice",
            LastName       = "Vet",
            OrganizationId = _testOrgId,
            AccountStatus  = UserAccountStatus.Active,
            IsActive       = true
        };

        var staff2 = new ApplicationUser
        {
            Id             = "user-2",
            Email          = "vet2@clinicB.com",
            FirstName      = "Bob",
            LastName       = "OtherOrg",
            OrganizationId = otherOrgId,
            AccountStatus  = UserAccountStatus.Active,
            IsActive       = true
        };

        _dbContext.Users.AddRange(staff1, staff2);
        await _dbContext.SaveChangesAsync();

        _userManagerMock.Setup(m => m.GetRolesAsync(staff1))
            .ReturnsAsync(new List<string> { Roles.Veterinarian });

        // Act
        var result = await _staffService.GetOrganizationStaffAsync(null, null, null);

        // Assert — only staff from _testOrgId is returned
        result.Should().HaveCount(1);
        result.First().Email.Should().Be("vet1@clinicA.com");
    }

    [Fact]
    public async Task GetStaffMemberById_CrossOrgUser_ThrowsKeyNotFoundException()
    {
        // Arrange
        var otherOrgId = Guid.NewGuid();
        var alienStaff = new ApplicationUser
        {
            Id             = "alien-user-id",
            Email          = "alien@otherclinic.com",
            FirstName      = "Eve",
            LastName       = "Alien",
            OrganizationId = otherOrgId,
            AccountStatus  = UserAccountStatus.Active
        };

        _dbContext.Users.Add(alienStaff);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = () => _staffService.GetStaffMemberByIdAsync("alien-user-id");

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>()
            .WithMessage("*not found in your organization*");
    }

    [Fact]
    public async Task CreateStaffMember_ValidVeterinarian_CreatesAndAssignsOrgId()
    {
        // Arrange
        var request = new CreateStaffUserRequestDto(
            FirstName:   "Marcus",
            LastName:    "Welby",
            Email:       "marcus@clinicA.com",
            Role:        Roles.Veterinarian,
            Password:    "SecurePass123!",
            PhoneNumber: "555-9876"
        );

        _userManagerMock
            .Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync((ApplicationUser?)null);

        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Success);

        _userManagerMock
            .Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), request.Role))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await _staffService.CreateStaffMemberAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Email.Should().Be("marcus@clinicA.com");
        result.Role.Should().Be(Roles.Veterinarian);
        result.OrganizationId.Should().Be(_testOrgId);

        _userManagerMock.Verify(m => m.CreateAsync(
            It.Is<ApplicationUser>(u => u.OrganizationId == _testOrgId && u.AccountStatus == UserAccountStatus.Active),
            request.Password), Times.Once);

        _userManagerMock.Verify(m => m.AddToRoleAsync(
            It.IsAny<ApplicationUser>(), Roles.Veterinarian), Times.Once);
    }

    [Fact]
    public async Task CreateStaffMember_DisallowedRole_ThrowsInvalidOperationException()
    {
        // Arrange
        var request = new CreateStaffUserRequestDto(
            FirstName: "Bad",
            LastName:  "Attempt",
            Email:     "admin@clinicA.com",
            Role:      Roles.SuperAdmin, // Forbidden!
            Password:  "SecurePass123!"
        );

        // Act
        var act = () => _staffService.CreateStaffMemberAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*only create 'Veterinarian' or 'InventoryOfficer'*");
    }

    [Fact]
    public async Task UpdateStaffStatus_DisablesAccount_PreservesUserRecord()
    {
        // Arrange
        var staff = new ApplicationUser
        {
            Id             = "staff-to-disable",
            Email          = "officer@clinicA.com",
            FirstName      = "Oscar",
            LastName       = "Officer",
            OrganizationId = _testOrgId,
            AccountStatus  = UserAccountStatus.Active,
            IsActive       = true
        };

        _dbContext.Users.Add(staff);
        await _dbContext.SaveChangesAsync();

        _userManagerMock
            .Setup(m => m.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        _userManagerMock
            .Setup(m => m.GetRolesAsync(staff))
            .ReturnsAsync(new List<string> { Roles.InventoryOfficer });

        // Act
        var result = await _staffService.UpdateStaffStatusAsync(
            "staff-to-disable",
            new UpdateStaffStatusRequestDto(UserAccountStatus.Disabled));

        // Assert
        result.Status.Should().Be("Disabled");

        var userInDb = await _dbContext.Users.FindAsync("staff-to-disable");
        userInDb.Should().NotBeNull(); // Preserved, not deleted
        userInDb!.AccountStatus.Should().Be(UserAccountStatus.Disabled);
        userInDb.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateStaffStatus_OwnManagerAccount_ThrowsInvalidOperationException()
    {
        // Arrange
        var managerUser = new ApplicationUser
        {
            Id             = "manager-user-id", // Same as authenticated manager
            Email          = "manager@clinicA.com",
            FirstName      = "Boss",
            LastName       = "Manager",
            OrganizationId = _testOrgId,
            AccountStatus  = UserAccountStatus.Active,
            IsActive       = true
        };

        _dbContext.Users.Add(managerUser);
        await _dbContext.SaveChangesAsync();

        // Act
        var act = () => _staffService.UpdateStaffStatusAsync(
            "manager-user-id",
            new UpdateStaffStatusRequestDto(UserAccountStatus.Disabled));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot alter the status of your own administrator account*");
    }
}
