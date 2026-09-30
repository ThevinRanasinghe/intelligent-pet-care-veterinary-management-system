using PetCare.Application.DTOs.Consultations;

namespace PetCare.Application.Interfaces;

/// <summary>
/// Computes organization-level booking availability over the nine fixed
/// one-hour slots (see <see cref="PetCare.Domain.Constants.BookingRules"/>).
/// A slot is bookable while at least one active veterinarian of the
/// organization has no overlapping non-cancelled appointment.
/// </summary>
public interface IBookingAvailabilityService
{
    /// <summary>
    /// Slot-by-slot availability for one day. When
    /// <paramref name="veterinarianId"/> is supplied, availability is
    /// evaluated against that veterinarian only (manager assign flow).
    /// </summary>
    Task<DayAvailabilityDto> GetDayAvailabilityAsync(
        Guid organizationId,
        DateOnly date,
        Guid? veterinarianId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Month overview used to render the booking calendar: per day flags for
    /// past / fully-booked / available.
    /// </summary>
    Task<IReadOnlyList<MonthAvailabilityDayDto>> GetMonthAvailabilityAsync(
        Guid organizationId,
        int year,
        int month,
        CancellationToken cancellationToken = default);
}
