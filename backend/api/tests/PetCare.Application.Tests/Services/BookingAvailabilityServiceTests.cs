using Moq;
using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;
using PetCare.Application.Services;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;
using Xunit;

namespace PetCare.Application.Tests.Services;

/// <summary>
/// Org-level slot availability: a one-hour slot is bookable while at least
/// one active veterinarian of the organization has no overlapping
/// non-cancelled appointment.
/// </summary>
public class BookingAvailabilityServiceTests
{
    private readonly Mock<IOrganizationRepository> _organizations = new();
    private readonly Mock<IVeterinarianRepository> _veterinarians = new();
    private readonly Mock<IAppointmentRepository> _appointments = new();

    private static readonly Guid OrgId = Guid.NewGuid();
    private static readonly Guid VetA = Guid.NewGuid();
    private static readonly Guid VetB = Guid.NewGuid();

    // A fixed future date keeps the "past slot" rules out of the tests.
    private static readonly DateOnly FutureDate =
        DateOnly.FromDateTime(DateTime.Today.AddDays(30));

    private BookingAvailabilityService CreateService() => new(
        _organizations.Object,
        _veterinarians.Object,
        _appointments.Object);

    private void SetupOrg(bool active = true)
    {
        _organizations.Setup(r => r.GetByIdAsync(OrgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Organization
            {
                Id = OrgId,
                Name = "Happy Paws",
                Status = active ? OrganizationStatus.Active : OrganizationStatus.Pending,
                IsActive = active
            });
    }

    private void SetupVets(params Guid[] ids)
    {
        _veterinarians.Setup(r => r.GetActiveByOrganizationAsync(OrgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ids.Select(id => new Veterinarian
            {
                Id = id,
                Name = $"Dr. {id.ToString()[..4]}",
                Active = true,
                OrganizationId = OrgId
            }).ToList());
    }

    private void SetupAppointments(params Appointment[] appointments)
    {
        _appointments.Setup(r => r.GetActiveByVeterinariansAndRangeAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(appointments.ToList());
    }

    private static Appointment BusyAppointment(Guid vetId, DateOnly date, int startHour) => new()
    {
        Id = Guid.NewGuid(),
        VeterinarianId = vetId,
        Date = date,
        StartTime = new TimeOnly(startHour, 0),
        EndTime = new TimeOnly(startHour + 1, 0),
        Status = AppointmentStatus.Confirmed
    };

    [Fact]
    public async Task GetDayAvailability_AllVetsFree_NineAvailableSlots()
    {
        SetupOrg();
        SetupVets(VetA, VetB);
        SetupAppointments();

        var result = await CreateService().GetDayAvailabilityAsync(OrgId, FutureDate);

        Assert.Equal(9, result.Slots.Count);
        Assert.False(result.IsPast);
        Assert.All(result.Slots, s =>
        {
            Assert.True(s.Available);
            Assert.Equal(2, s.AvailableVeterinarianIds.Count);
        });
        Assert.Equal("09:00", result.Slots[0].Start);
        Assert.Equal("10:00", result.Slots[0].End);
        Assert.Equal("17:00", result.Slots[8].Start);
        Assert.Equal("18:00", result.Slots[8].End);
    }

    [Fact]
    public async Task GetDayAvailability_OneVetBusy_SlotListsOnlyOtherVets()
    {
        SetupOrg();
        SetupVets(VetA, VetB);
        SetupAppointments(BusyAppointment(VetA, FutureDate, 10));

        var result = await CreateService().GetDayAvailabilityAsync(OrgId, FutureDate);

        var tenToEleven = result.Slots.Single(s => s.Start == "10:00");
        Assert.True(tenToEleven.Available);
        Assert.Equal(new List<Guid> { VetB }, tenToEleven.AvailableVeterinarianIds);

        var nineToTen = result.Slots.Single(s => s.Start == "09:00");
        Assert.Equal(2, nineToTen.AvailableVeterinarianIds.Count);
    }

    [Fact]
    public async Task GetDayAvailability_AllVetsBusy_SlotUnavailable()
    {
        SetupOrg();
        SetupVets(VetA, VetB);
        SetupAppointments(
            BusyAppointment(VetA, FutureDate, 10),
            BusyAppointment(VetB, FutureDate, 10));

        var result = await CreateService().GetDayAvailabilityAsync(OrgId, FutureDate);

        var tenToEleven = result.Slots.Single(s => s.Start == "10:00");
        Assert.False(tenToEleven.Available);
        Assert.Empty(tenToEleven.AvailableVeterinarianIds);
    }

    [Fact]
    public async Task GetDayAvailability_SpecificVeterinarian_OnlyChecksThatVet()
    {
        SetupOrg();
        SetupVets(VetA, VetB);
        // VetA busy 10-11; VetB free — but the query asks only about VetA.
        SetupAppointments(BusyAppointment(VetA, FutureDate, 10));

        var result = await CreateService().GetDayAvailabilityAsync(OrgId, FutureDate, VetA);

        var tenToEleven = result.Slots.Single(s => s.Start == "10:00");
        Assert.False(tenToEleven.Available);
        var nineToTen = result.Slots.Single(s => s.Start == "09:00");
        Assert.True(nineToTen.Available);
        Assert.Equal(new List<Guid> { VetA }, nineToTen.AvailableVeterinarianIds);
    }

    [Fact]
    public async Task GetDayAvailability_PastDate_AllSlotsUnavailable()
    {
        SetupOrg();
        SetupVets(VetA);
        SetupAppointments();

        var past = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
        var result = await CreateService().GetDayAvailabilityAsync(OrgId, past);

        Assert.True(result.IsPast);
        Assert.All(result.Slots, s => Assert.False(s.Available));
    }

    [Fact]
    public async Task GetDayAvailability_InactiveOrganization_ThrowsNotFound()
    {
        SetupOrg(active: false);

        await Assert.ThrowsAsync<NotFoundException>(
            () => CreateService().GetDayAvailabilityAsync(OrgId, FutureDate));
    }

    [Fact]
    public async Task GetMonthAvailability_FullyBookedDay_Reported()
    {
        SetupOrg();
        SetupVets(VetA);

        // VetA busy all nine slots on day 10 of the month of FutureDate.
        var busyDay = new DateOnly(FutureDate.Year, FutureDate.Month, 10);
        var appointments = BookingSlotHours()
            .Select(h => BusyAppointment(VetA, busyDay, h))
            .ToArray();
        SetupAppointments(appointments);

        var days = await CreateService().GetMonthAvailabilityAsync(
            OrgId, FutureDate.Year, FutureDate.Month);

        var expectedDays = DateTime.DaysInMonth(FutureDate.Year, FutureDate.Month);
        Assert.Equal(expectedDays, days.Count);

        var booked = days.Single(d => d.Date == busyDay.ToString("yyyy-MM-dd"));
        Assert.True(booked.FullyBooked);
        Assert.False(booked.Available);
        Assert.False(booked.IsPast);

        var other = days.Single(d => d.Date == busyDay.AddDays(1).ToString("yyyy-MM-dd"));
        Assert.True(other.Available);
        Assert.False(other.FullyBooked);
    }

    [Fact]
    public async Task GetMonthAvailability_PastDays_MarkedIsPast()
    {
        SetupOrg();
        SetupVets(VetA);
        SetupAppointments();

        var days = await CreateService().GetMonthAvailabilityAsync(
            OrgId, DateTime.Today.Year, DateTime.Today.Month);

        var yesterday = DateTime.Today.AddDays(-1);
        if (yesterday.Month == DateTime.Today.Month)
        {
            var entry = days.Single(d => d.Date == yesterday.ToString("yyyy-MM-dd"));
            Assert.True(entry.IsPast);
            Assert.False(entry.Available);
            Assert.False(entry.FullyBooked);
        }
    }

    private static IEnumerable<int> BookingSlotHours()
    {
        for (var h = 9; h <= 17; h++)
        {
            yield return h;
        }
    }
}
