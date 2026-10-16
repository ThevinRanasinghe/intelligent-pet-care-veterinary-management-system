using FluentValidation;
using Moq;
using PetCare.Application.DTOs.Scheduling;
using PetCare.Application.Interfaces;
using PetCare.Application.Validators;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;
using Xunit;

namespace PetCare.Application.Tests.Validators;

public class CreateAppointmentRequestValidatorTests
{
    // TC-BE-008: appointment start must be strictly before its end.
    [Theory]
    [InlineData(10, 10)] // Start == end (10:00 - 10:00)
    [InlineData(11, 10)] // Start > end  (11:00 - 10:00)
    public async Task CreateAppointment_InvalidStartAndEnd_FailsValidation(
        int startHour, int endHour)
    {
        // ARRANGE: valid IDs and repository responses, but invalid appointment times.
        var veterinarianId = Guid.NewGuid();
        var slotId = Guid.NewGuid();

        var veterinarianRepository = new Mock<IVeterinarianRepository>();
        veterinarianRepository
            .Setup(r => r.GetByIdAsync(veterinarianId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Veterinarian
            {
                Id = veterinarianId,
                Name = "Test Veterinarian",
                Specialisation = "General",
                Branch = "Colombo",
                Active = true
            });

        var slotRepository = new Mock<IAppointmentSlotRepository>();
        slotRepository
            .Setup(r => r.GetByIdAsync(slotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppointmentSlot
            {
                Id = slotId,
                VeterinarianId = veterinarianId,
                Date = new DateOnly(2030, 1, 10),
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(12, 0),
                Branch = "Colombo",
                Status = AppointmentSlotStatus.Available
            });

        var request = new CreateAppointmentRequest
        {
            PetId = "PET-TEST0001",
            VeterinarianId = veterinarianId,
            AppointmentSlotId = slotId,
            ScheduledStart = new DateTime(2030, 1, 10, startHour, 0, 0),
            ScheduledEnd = new DateTime(2030, 1, 10, endHour, 0, 0)
        };

        var validator = new CreateAppointmentRequestValidator(
            veterinarianRepository.Object,
            slotRepository.Object);

        // ACT: run the actual FluentValidation validator.
        var result = await validator.ValidateAsync(request);

        // ASSERT: the time-order rule rejects the request.
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.PropertyName == nameof(CreateAppointmentRequest.ScheduledStart) &&
            error.ErrorMessage == "ScheduledStart must be before ScheduledEnd.");
    }

    // TC-BE-009: appointments must start and end on the same calendar day.
    [Fact]
    public async Task CreateAppointment_SpansMultipleDays_FailsValidation()
    {
        // ARRANGE: valid veterinarian and slot IDs; only the date range is invalid.
        var veterinarianId = Guid.NewGuid();
        var slotId = Guid.NewGuid();

        var veterinarianRepository = new Mock<IVeterinarianRepository>();
        veterinarianRepository
            .Setup(r => r.GetByIdAsync(veterinarianId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Veterinarian
            {
                Id = veterinarianId,
                Name = "Test Veterinarian",
                Specialisation = "General",
                Branch = "Colombo",
                Active = true
            });

        var slotRepository = new Mock<IAppointmentSlotRepository>();
        slotRepository
            .Setup(r => r.GetByIdAsync(slotId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AppointmentSlot
            {
                Id = slotId,
                VeterinarianId = veterinarianId,
                Date = new DateOnly(2030, 1, 10),
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(12, 0),
                Branch = "Colombo",
                Status = AppointmentSlotStatus.Available
            });

        var request = new CreateAppointmentRequest
        {
            PetId = "PET-TEST0001",
            VeterinarianId = veterinarianId,
            AppointmentSlotId = slotId,
            ScheduledStart = new DateTime(2030, 1, 10, 10, 0, 0),
            ScheduledEnd = new DateTime(2030, 1, 11, 10, 30, 0)
        };

        var validator = new CreateAppointmentRequestValidator(
            veterinarianRepository.Object,
            slotRepository.Object);

        // ACT: validate the request using the real FluentValidation rules.
        var result = await validator.ValidateAsync(request);

        // ASSERT: start < end is true, but crossing calendar days is forbidden.
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.ErrorMessage == "An appointment cannot span multiple days.");
        Assert.DoesNotContain(result.Errors, error =>
            error.ErrorMessage == "ScheduledStart must be before ScheduledEnd.");
    }
}
