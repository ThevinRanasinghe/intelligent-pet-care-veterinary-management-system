using PetCare.Application.DTOs.Clinics;
using PetCare.Application.Interfaces;
using PetCare.Domain.Enums;

namespace PetCare.Application.Services;

/// <inheritdoc cref="IClinicLocatorService"/>
public class ClinicLocatorService : IClinicLocatorService
{
    private readonly IOrganizationRepository _organizations;

    public ClinicLocatorService(IOrganizationRepository organizations)
    {
        _organizations = organizations;
    }

    public async Task<List<NearbyClinicResponse>> FindNearbyAsync(
        double latitude,
        double longitude,
        double radiusKm = 50,
        CancellationToken cancellationToken = default)
    {
        var organizations = await _organizations.GetAllAsync(cancellationToken);

        return organizations
            .Where(o => o.Status == OrganizationStatus.Active
                        && o.IsActive
                        && o.Latitude.HasValue
                        && o.Longitude.HasValue)
            .Select(o => new NearbyClinicResponse
            {
                Id = o.Id,
                Name = o.Name,
                Address = o.Address,
                City = o.City,
                Latitude = o.Latitude!.Value,
                Longitude = o.Longitude!.Value,
                DistanceKm = CalculateDistanceKm(
                    latitude, longitude, o.Latitude!.Value, o.Longitude!.Value),
            })
            .Where(c => c.DistanceKm <= radiusKm)
            .OrderBy(c => c.DistanceKm)
            .ToList();
    }

    private static double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371;

        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);

        var a =
            Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(DegreesToRadians(lat1)) *
            Math.Cos(DegreesToRadians(lat2)) *
            Math.Sin(dLon / 2) *
            Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return Math.Round(earthRadiusKm * c, 2);
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }
}
