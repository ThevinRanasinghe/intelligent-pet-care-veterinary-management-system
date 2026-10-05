using PetCare.Application.DTOs.Clinics;

namespace PetCare.Application.Interfaces;

/// <summary>
/// Finds active clinics with a stored location, ordered by straight-line
/// (Haversine) distance from a caller-supplied coordinate.
/// </summary>
public interface IClinicLocatorService
{
    /// <summary>
    /// Active clinics with both coordinates set, filtered to
    /// <paramref name="radiusKm"/> and sorted nearest-first.
    /// </summary>
    Task<List<NearbyClinicResponse>> FindNearbyAsync(
        double latitude,
        double longitude,
        double radiusKm = 50,
        CancellationToken cancellationToken = default);
}
