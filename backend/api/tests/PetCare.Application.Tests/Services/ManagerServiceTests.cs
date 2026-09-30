using FluentValidation;
using Moq;
using PetCare.Application.DTOs.Manager;
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
/// Tests for ClinicManager staff account creation (Veterinarian /
/// InventoryOfficer). The organization is resolved from the caller's
/// tenant context — the request cannot steer an account into another
/// organization.
/// </summary>
public class ManagerServiceTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IOrganizationRepository> _organizations = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly IValidator<ManagerCreateStaffRequest> _validator = new ManagerCreateStaffRequestValidator();
    private readonly Mock<IVeterinarianRepository> _veterinarians = new();
    private readonly Mock<IAppointmentRepository> _appointments = new();
    private readonly Mock<IExaminationRepository> _examinations = new();
    private readonly Mock<IPrescriptionRepository> _prescriptions = new();
    private readonly Mock<IQuotationRepository> _quotations = new();

    private static readonly Guid ManagerOrgId = Guid.NewGuid();

    private ManagerService CreateService() => new(
        _users.Object,
        _organizations.Object,
        _passwordHasher.Object,
        _tenant.Object,
        _validator,
        _veterinarians.Object,
        _appointments.Object,
        _examinations.Object,
        _prescriptions.Object,
        _quotations.Object);

    private static Organization ManagerOrg() => new()
    {
        Id = ManagerOrgId,
        Name = "Happy Paws Clinic",
        Email = "clinic@example.com",
        Status = OrganizationStatus.Active,
        IsActive = true,
    };

    private static ManagerCreateStaffRequest ValidRequest() => new()
    {
        FirstName = "Nimal",
        LastName = "Perera",
        Email = "nimal.perera@example.com",
        PhoneNumber = "0771234567",
    };

    private void SetupScopedManager()
    {
        _tenant.Setup(t => t.GetOrganizationIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ManagerOrgId);
        _organizations.Setup(o => o.GetByIdAsync(ManagerOrgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ManagerOrg());
        _users.Setup(u => u.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _passwordHasher.Setup(h => h.HashPassword(It.IsAny<string>()))
            .Returns("hashed-temp-password");
    }

    [Fact]
    public async Task CreateVeterinarian_ScopedManager_CreatesVetInOwnOrg()
    {
        SetupScopedManager();
        var service = CreateService();

        var result = await service.CreateVeterinarianAsync(ValidRequest());

        Assert.Equal(Roles.Veterinarian, result.Role);
        Assert.Equal(ManagerOrgId, result.OrganizationId);
        Assert.Equal("Happy Paws Clinic", result.OrganizationName);
        Assert.True(result.Active);
        Assert.True(result.MustChangePassword);
        Assert.False(string.IsNullOrWhiteSpace(result.TemporaryPassword));

        _users.Verify(u => u.AddAsync(
            It.Is<User>(x =>
                x.Role == Roles.Veterinarian &&
                x.OrganizationId == ManagerOrgId &&
                x.MustChangePassword &&
                x.Active &&
                x.PasswordHash == "hashed-temp-password"),
            It.IsAny<CancellationToken>()), Times.Once);
        _users.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateInventoryOfficer_ScopedManager_CreatesOfficerInOwnOrg()
    {
        SetupScopedManager();
        var service = CreateService();

        var result = await service.CreateInventoryOfficerAsync(ValidRequest());

        Assert.Equal(Roles.InventoryOfficer, result.Role);
        Assert.Equal(ManagerOrgId, result.OrganizationId);
        Assert.True(result.MustChangePassword);
        Assert.False(string.IsNullOrWhiteSpace(result.TemporaryPassword));
    }

    [Fact]
    public async Task CreateVeterinarian_RequestCannotChooseOrganization()
    {
        SetupScopedManager();
        var service = CreateService();
        var request = ValidRequest();

        // The request DTO has no OrganizationId member — the compiler itself
        // prevents a caller-supplied org. Defense-in-depth check: the created
        // user still lands in the manager's org.
        Assert.False(request.GetType().GetProperties().Any(p => p.Name == "OrganizationId"));

        var result = await service.CreateVeterinarianAsync(request);
        Assert.Equal(ManagerOrgId, result.OrganizationId);
    }

    [Fact]
    public async Task CreateVeterinarian_AlsoCreatesLinkedVeterinarianRow()
    {
        SetupScopedManager();
        User? createdUser = null;
        _users
            .Setup(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => createdUser = u);
        var service = CreateService();

        await service.CreateVeterinarianAsync(ValidRequest());

        _veterinarians.Verify(v => v.AddAsync(
            It.Is<Veterinarian>(x =>
                x.UserId == createdUser!.Id &&
                x.Name == "Nimal Perera" &&
                x.OrganizationId == ManagerOrgId &&
                x.Active &&
                x.Branch == "Happy Paws Clinic"),
            It.IsAny<CancellationToken>()), Times.Once);
        _users.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateInventoryOfficer_DoesNotCreateVeterinarianRow()
    {
        SetupScopedManager();
        var service = CreateService();

        await service.CreateInventoryOfficerAsync(ValidRequest());

        _veterinarians.Verify(v => v.AddAsync(It.IsAny<Veterinarian>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateVeterinarian_ManagerWithoutOrganization_Forbidden()
    {
        _tenant.Setup(t => t.GetOrganizationIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => service.CreateVeterinarianAsync(ValidRequest()));
        Assert.Contains("not linked to an organization", ex.Message);
        _users.Verify(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateInventoryOfficer_InactiveOrganization_Forbidden()
    {
        _tenant.Setup(t => t.GetOrganizationIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ManagerOrgId);
        var suspended = ManagerOrg();
        suspended.Status = OrganizationStatus.Suspended;
        suspended.IsActive = false;
        _organizations.Setup(o => o.GetByIdAsync(ManagerOrgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(suspended);
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => service.CreateInventoryOfficerAsync(ValidRequest()));
        Assert.Contains("Active", ex.Message);
        _users.Verify(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateVeterinarian_DuplicateEmail_Rejected()
    {
        SetupScopedManager();
        _users.Setup(u => u.EmailExistsAsync("nimal.perera@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = CreateService();

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CreateVeterinarianAsync(ValidRequest()));
        Assert.Contains("email already exists", ex.Message, StringComparison.OrdinalIgnoreCase);
        _users.Verify(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateInventoryOfficer_InvalidRequest_ThrowsValidationException()
    {
        var service = CreateService();
        var request = ValidRequest() with { Email = "not-an-email" };

        await Assert.ThrowsAsync<ValidationException>(
            () => service.CreateInventoryOfficerAsync(request));
        _users.Verify(u => u.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
