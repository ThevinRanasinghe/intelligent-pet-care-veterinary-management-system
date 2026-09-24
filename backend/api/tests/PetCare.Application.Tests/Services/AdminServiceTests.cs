using FluentValidation;
using Moq;
using PetCare.Application.DTOs.Admin;
using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;
using PetCare.Application.Services;
using PetCare.Application.Validators;
using PetCare.Domain.Constants;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;
using Xunit;

namespace PetCare.Application.Tests.Services;

/// <summary>
/// Tests for Administrator-only staff account creation (Veterinarian /
/// InventoryOfficer). Role is fixed by the calling method — never taken
/// from the request — and OrganizationId is validated server-side.
/// </summary>
public class AdminServiceTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IOrganizationRepository> _organizations = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly IValidator<CreateStaffUserRequest> _validator = new CreateStaffUserRequestValidator();

    private static readonly Guid ActiveOrgId = Guid.NewGuid();

    private AdminService CreateService() => new(
        _users.Object,
        _organizations.Object,
        _passwordHasher.Object,
        _validator);

    private static Organization ActiveOrg() => new()
    {
        Id = ActiveOrgId,
        Name = "Happy Paws Clinic",
        Email = "clinic@example.com",
        Status = OrganizationStatus.Active,
        IsActive = true,
    };

    private static CreateStaffUserRequest ValidRequest() => new()
    {
        FirstName = "Nimal",
        LastName = "Perera",
        Email = "nimal.perera@example.com",
        PhoneNumber = "0771234567",
        OrganizationId = ActiveOrgId,
    };

    private void SetupValid()
    {
        _users.Setup(u => u.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _organizations.Setup(o => o.GetByIdAsync(ActiveOrgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveOrg());
        _passwordHasher.Setup(h => h.HashPassword(It.IsAny<string>()))
            .Returns("hashed-temp-password");
    }

    [Fact]
    public async Task CreateVeterinarian_ValidRequest_CreatesActiveVeterinarianInOrg()
    {
        SetupValid();
        var service = CreateService();

        var result = await service.CreateVeterinarianAsync(ValidRequest());

        Assert.Equal(Roles.Veterinarian, result.Role);
        Assert.Equal(ActiveOrgId, result.OrganizationId);
        Assert.Equal("Happy Paws Clinic", result.OrganizationName);
        Assert.True(result.Active);
        Assert.True(result.MustChangePassword);
        Assert.False(string.IsNullOrWhiteSpace(result.TemporaryPassword));

        _users.Verify(u => u.AddAsync(
            It.Is<User>(x =>
                x.Role == Roles.Veterinarian &&
                x.OrganizationId == ActiveOrgId &&
                x.MustChangePassword &&
                x.Active &&
                x.PasswordHash == "hashed-temp-password"),
            It.IsAny<CancellationToken>()), Times.Once);
        _users.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateInventoryOfficer_ValidRequest_CreatesActiveOfficerInOrg()
    {
        SetupValid();
        var service = CreateService();

        var result = await service.CreateInventoryOfficerAsync(ValidRequest());

        Assert.Equal(Roles.InventoryOfficer, result.Role);
        Assert.Equal(ActiveOrgId, result.OrganizationId);
        Assert.True(result.MustChangePassword);
        Assert.False(string.IsNullOrWhiteSpace(result.TemporaryPassword));
    }

    [Fact]
    public async Task CreateVeterinarian_DuplicateEmail_Rejected()
    {
        _users.Setup(u => u.EmailExistsAsync("nimal.perera@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateVeterinarianAsync(ValidRequest()));
        Assert.Contains("email already exists", ex.Message, StringComparison.OrdinalIgnoreCase);
        _users.Verify(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateVeterinarian_NonexistentOrganization_ThrowsNotFound()
    {
        _users.Setup(u => u.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _organizations.Setup(o => o.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Organization?)null);
        var service = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.CreateVeterinarianAsync(ValidRequest()));
        _users.Verify(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateInventoryOfficer_PendingOrganization_Rejected()
    {
        _users.Setup(u => u.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var pendingOrg = ActiveOrg();
        pendingOrg.Status = OrganizationStatus.Pending;
        pendingOrg.IsActive = false;
        _organizations.Setup(o => o.GetByIdAsync(ActiveOrgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendingOrg);
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateInventoryOfficerAsync(ValidRequest()));
        Assert.Contains("Active organization", ex.Message);
        _users.Verify(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateInventoryOfficer_SuspendedOrganization_Rejected()
    {
        _users.Setup(u => u.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var suspended = ActiveOrg();
        suspended.Status = OrganizationStatus.Suspended;
        _organizations.Setup(o => o.GetByIdAsync(ActiveOrgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(suspended);
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateInventoryOfficerAsync(ValidRequest()));
    }

    [Fact]
    public async Task CreateVeterinarian_InvalidRequest_ThrowsValidationException()
    {
        var service = CreateService();
        var request = ValidRequest() with { Email = "not-an-email" };

        await Assert.ThrowsAsync<ValidationException>(
            () => service.CreateVeterinarianAsync(request));
        _users.Verify(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateVeterinarian_EmptyOrganizationId_ThrowsValidationException()
    {
        var service = CreateService();
        var request = ValidRequest() with { OrganizationId = Guid.Empty };

        await Assert.ThrowsAsync<ValidationException>(
            () => service.CreateVeterinarianAsync(request));
    }
}
