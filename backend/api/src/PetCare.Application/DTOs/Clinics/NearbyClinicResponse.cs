namespace PetCare.Application.DTOs.Clinics;

/// <summary>An active clinic with a known location and its straight-line
/// distance from the caller's coordinate.</summary>
public record NearbyClinicResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    /// <summary>Straight-line Haversine distance in kilometres, rounded to 2dp.</summary>
    public double DistanceKm { get; init; }
}
