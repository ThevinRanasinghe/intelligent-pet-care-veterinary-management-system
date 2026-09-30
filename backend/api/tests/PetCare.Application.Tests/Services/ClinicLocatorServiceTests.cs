using Moq;
using PetCare.Application.Interfaces;
using PetCare.Application.Services;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;
using Xunit;

namespace PetCare.Application.Tests.Services;

/// <summary>
/// ClinicLocatorService: only active organizations with both coordinates
/// are returned, Haversine distances are rounded, filtered by radius and
/// sorted nearest-first.
/// </summary>
public class ClinicLocatorServiceTests
{
    private readonly Mock<IOrganizationRepository> _organizations = new();

    private ClinicLocatorService CreateService() => new(_organizations.Object);

    private static Organization ActiveClinic(
        string name,
        double latitude,
        double longitude,
        string address = "1 Clinic Road",
        string city = "Colombo") => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Address = address,
        City = city,
        Latitude = latitude,
        Longitude = longitude,
        Status = OrganizationStatus.Active,
        IsActive = true,
    };

    private void SetupOrgs(params Organization[] orgs)
    {
        _organizations.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(orgs.ToList());
    }

    [Fact]
    public async Task FindNearby_ReturnsActiveClinicWithCoordinatesAndMappedFields()
    {
        var clinic = ActiveClinic("Colombo Pet Clinic", 6.93, 79.86, "5 Galle Road", "Colombo");
        SetupOrgs(clinic);

        var result = await CreateService().FindNearbyAsync(6.9271, 79.8612);

        var item = Assert.Single(result);
        Assert.Equal(clinic.Id, item.Id);
        Assert.Equal("Colombo Pet Clinic", item.Name);
        Assert.Equal("5 Galle Road", item.Address);
        Assert.Equal("Colombo", item.City);
        Assert.Equal(6.93, item.Latitude);
        Assert.Equal(79.86, item.Longitude);
        Assert.True(item.DistanceKm >= 0);
    }

    [Fact]
    public async Task FindNearby_ExcludesPendingInactiveAndCoordinatelessClinics()
    {
        SetupOrgs(
            ActiveClinic("Eligible", 6.93, 79.86),
            new Organization
            {
                Id = Guid.NewGuid(),
                Name = "Pending Clinic",
                Latitude = 6.93,
                Longitude = 79.86,
                Status = OrganizationStatus.Pending,
                IsActive = false,
            },
            new Organization
            {
                Id = Guid.NewGuid(),
                Name = "Deactivated Clinic",
                Latitude = 6.93,
                Longitude = 79.86,
                Status = OrganizationStatus.Active,
                IsActive = false,
            },
            new Organization
            {
                Id = Guid.NewGuid(),
                Name = "No Location Clinic",
                Status = OrganizationStatus.Active,
                IsActive = true,
            });

        var result = await CreateService().FindNearbyAsync(6.9271, 79.8612);

        var item = Assert.Single(result);
        Assert.Equal("Eligible", item.Name);
    }

    [Fact]
    public async Task FindNearby_SortsNearestFirstAndRoundsDistance()
    {
        var far = ActiveClinic("Far", 7.2906, 80.6337);   // ~40km from Colombo
        var near = ActiveClinic("Near", 6.95, 79.85);     // ~3km from Colombo
        SetupOrgs(far, near);

        var result = await CreateService().FindNearbyAsync(6.9271, 79.8612, radiusKm: 200);

        Assert.Equal(2, result.Count);
        Assert.Equal("Near", result[0].Name);
        Assert.Equal("Far", result[1].Name);
        Assert.True(result[0].DistanceKm < result[1].DistanceKm);
        Assert.Equal(result[0].DistanceKm, Math.Round(result[0].DistanceKm, 2));
        Assert.True(result[0].DistanceKm > 0);
    }

    [Fact]
    public async Task FindNearby_FiltersByRadiusKm()
    {
        SetupOrgs(
            ActiveClinic("Near", 6.95, 79.85),
            ActiveClinic("Far", 7.2906, 80.6337));

        var result = await CreateService().FindNearbyAsync(6.9271, 79.8612, radiusKm: 10);

        var item = Assert.Single(result);
        Assert.Equal("Near", item.Name);
        Assert.True(item.DistanceKm <= 10);
    }

    [Fact]
    public async Task FindNearby_WhenNoEligibleClinics_ReturnsEmptyList()
    {
        SetupOrgs(new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Pending Clinic",
            Status = OrganizationStatus.Pending,
        });

        var result = await CreateService().FindNearbyAsync(6.9271, 79.8612);

        Assert.Empty(result);
    }
}
