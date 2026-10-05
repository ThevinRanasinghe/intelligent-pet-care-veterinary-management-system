using PetCare.Application.DTOs.Consultations;
using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;

namespace PetCare.Application.Services;

/// <summary>
/// Org-level availability over the fixed one-hour slots. One grouped
/// appointment query per date range — never a per-day/per-vet query loop.
/// </summary>
public class BookingAvailabilityService : IBookingAvailabilityService
{
    private readonly IOrganizationRepository _organizations;
    private readonly IVeterinarianRepository _veterinarians;
    private readonly IAppointmentRepository _appointments;

    public BookingAvailabilityService(
        IOrganizationRepository organizations,
        IVeterinarianRepository veterinarians,
        IAppointmentRepository appointments)
    {
        _organizations = organizations;
        _veterinarians = veterinarians;
        _appointments = appointments;
    }

    public async Task<DayAvailabilityDto> GetDayAvailabilityAsync(
        Guid organizationId,
        DateOnly date,
        Guid? veterinarianId = null,
        CancellationToken cancellationToken = default)
    {
        await RequireActiveOrganizationAsync(organizationId, cancellationToken);

        var veterinarians = await GetCandidatesAsync(
            organizationId, veterinarianId, cancellationToken);

        var veterinarianIds = veterinarians.Select(v => v.Id).ToList();

        var appointments = await _appointments.GetActiveByVeterinariansAndRangeAsync(
            veterinarianIds, date, date, cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.Now);
        var now = TimeOnly.FromDateTime(DateTime.Now);
        var isPast = date < today;

        var slots = BookingRules.DaySlots
            .Select(slot =>
            {
                var freeVeterinarianIds = veterinarians
                    .Where(v => !Overlaps(appointments, v.Id, slot.Start, slot.End))
                    .Select(v => v.Id)
                    .ToList();

                // A slot that has already started (or passed) today cannot
                // be booked anymore.
                var expiredToday = date == today && slot.Start <= now;

                var available = !isPast && !expiredToday && freeVeterinarianIds.Count > 0;

                return new AvailabilitySlotDto
                {
                    Start = slot.Start.ToString("HH:mm"),
                    End = slot.End.ToString("HH:mm"),
                    Available = available,
                    AvailableVeterinarianIds = available ? freeVeterinarianIds : new List<Guid>()
                };
            })
            .ToList();

        return new DayAvailabilityDto
        {
            Date = date.ToString("yyyy-MM-dd"),
            IsPast = isPast,
            Slots = slots
        };
    }

    public async Task<IReadOnlyList<MonthAvailabilityDayDto>> GetMonthAvailabilityAsync(
        Guid organizationId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        await RequireActiveOrganizationAsync(organizationId, cancellationToken);

        var veterinarians = await _veterinarians
            .GetActiveByOrganizationAsync(organizationId, cancellationToken);
        var veterinarianIds = veterinarians.Select(v => v.Id).ToList();

        var daysInMonth = DateTime.DaysInMonth(year, month);
        var from = new DateOnly(year, month, 1);
        var to = new DateOnly(year, month, daysInMonth);

        // One grouped query for the whole month — appointments for every
        // active veterinarian of the organization.
        var appointments = await _appointments.GetActiveByVeterinariansAndRangeAsync(
            veterinarianIds, from, to, cancellationToken);

        var appointmentsByDate = appointments
            .GroupBy(a => a.Date)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Appointment>)g.ToList());

        var today = DateOnly.FromDateTime(DateTime.Now);

        var days = new List<MonthAvailabilityDayDto>(daysInMonth);
        for (var day = 1; day <= daysInMonth; day++)
        {
            var date = new DateOnly(year, month, day);
            var isPast = date < today;

            var fullyBooked = false;
            if (!isPast)
            {
                appointmentsByDate.TryGetValue(date, out var dayAppointments);
                dayAppointments ??= Array.Empty<Appointment>();

                fullyBooked = BookingRules.DaySlots.All(slot =>
                    veterinarians.All(v =>
                        Overlaps(dayAppointments, v.Id, slot.Start, slot.End)));
            }

            days.Add(new MonthAvailabilityDayDto
            {
                Date = date.ToString("yyyy-MM-dd"),
                IsPast = isPast,
                FullyBooked = !isPast && fullyBooked,
                Available = !isPast && !fullyBooked
            });
        }

        return days;
    }

    private async Task<Organization> RequireActiveOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var organization = await _organizations.GetByIdAsync(organizationId, cancellationToken);

        if (organization is null
            || organization.Status != OrganizationStatus.Active
            || !organization.IsActive)
        {
            throw new NotFoundException(
                "The organization was not found or is not active.");
        }

        return organization;
    }

    /// <summary>
    /// Active veterinarians of the organization — or just the requested
    /// veterinarian when the manager assign flow asks for a specific one.
    /// </summary>
    private async Task<IReadOnlyList<Veterinarian>> GetCandidatesAsync(
        Guid organizationId,
        Guid? veterinarianId,
        CancellationToken cancellationToken)
    {
        var veterinarians = await _veterinarians
            .GetActiveByOrganizationAsync(organizationId, cancellationToken);

        if (veterinarianId is null)
        {
            return veterinarians;
        }

        // A veterinarian outside the organization yields no candidates —
        // every slot is reported unavailable.
        return veterinarians
            .Where(v => v.Id == veterinarianId.Value)
            .ToList();
    }

    /// <summary>True when the veterinarian has a non-cancelled appointment
    /// overlapping [start, end) among the pre-fetched appointments.</summary>
    private static bool Overlaps(
        IReadOnlyList<Appointment> appointments,
        Guid veterinarianId,
        TimeOnly start,
        TimeOnly end) =>
        appointments.Any(a =>
            a.VeterinarianId == veterinarianId
            && a.Status != AppointmentStatus.Cancelled
            && a.StartTime < end
            && a.EndTime > start);
}
