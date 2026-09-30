using Microsoft.EntityFrameworkCore;
using PetCare.Application.DTOs;
using PetCare.Application.Exceptions;
using PetCare.Domain.Constants;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;
using PetCare.Infrastructure;
using PetCare.Infrastructure.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace PetCare.Tests;

/// <summary>
/// InMemory-backed tests for the appointment-completion path of
/// ExaminationService.CreateAsync: vet identity resolution, appointment
/// ownership/state checks, and link propagation.
/// </summary>
public class ExaminationAppointmentTests
{
    private static PetCareDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<PetCareDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PetCareDbContext(options);
    }

    private static TestTenantContext VetCaller(Guid userId) => new()
    {
        UserId = userId,
        Role = Roles.Veterinarian,
        IsPlatformAdmin = false
    };

    private static Veterinarian LinkedVeterinarian(Guid userId) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Dr. Vet",
        UserId = userId,
        Active = true
    };

    private static (Appointment appointment, AppointmentSlot slot) ConfirmedAppointment(Veterinarian vet)
    {
        var slot = new AppointmentSlot
        {
            Id = Guid.NewGuid(),
            VeterinarianId = vet.Id,
            Date = new DateOnly(2025, 6, 5),
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(11, 0),
            Branch = "Main",
            Status = AppointmentSlotStatus.Reserved
        };
        var appointment = new Appointment
        {
            Id = Guid.NewGuid(),
            PetId = "PET-TEST0001",
            VeterinarianId = vet.Id,
            AppointmentSlotId = slot.Id,
            Date = slot.Date,
            StartTime = slot.StartTime,
            EndTime = slot.EndTime,
            Status = AppointmentStatus.Confirmed,
            ConsultationRequestId = "CON-TEST0001"
        };
        return (appointment, slot);
    }

    [Fact]
    public async Task CreateAsync_WithAppointment_CompletesAppointmentAndSetsLinks()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var userId = Guid.NewGuid();
        var vet = LinkedVeterinarian(userId);
        context.Veterinarians.Add(vet);
        var (appointment, slot) = ConfirmedAppointment(vet);
        context.AppointmentSlots.Add(slot);
        context.Appointments.Add(appointment);
        await context.SaveChangesAsync();

        var service = new ExaminationService(context, VetCaller(userId));
        var dto = new CreateExaminationDto
        {
            PetId = "PET-IGNORED", // taken from the appointment
            VeterinarianId = Guid.NewGuid(), // ignored for vet callers
            AppointmentId = appointment.Id,
            VeterinarianCharge = 2500m,
            Symptoms = "Fever",
            Notes = "Checkup",
            ExaminationDate = DateTime.UtcNow
        };

        // Act
        var result = await service.CreateAsync(dto);

        // Assert
        Assert.Equal(appointment.Id, result.AppointmentId);
        Assert.Equal(vet.Id, result.VeterinarianId);
        Assert.Equal(2500m, result.VeterinarianCharge);

        var saved = await context.Examinations.FindAsync(result.Id);
        Assert.NotNull(saved);
        Assert.Equal(appointment.Id, saved!.AppointmentId);
        Assert.Equal("PET-TEST0001", saved.PetId);
        Assert.Equal("CON-TEST0001", saved.ConsultationRequestId);

        Assert.Equal(AppointmentStatus.Completed, appointment.Status);
        Assert.Equal(AppointmentSlotStatus.Completed, slot.Status);
    }

    [Fact]
    public async Task CreateAsync_AppointmentOwnedByAnotherVeterinarian_ThrowsConflict()
    {
        using var context = GetInMemoryDbContext();
        var userId = Guid.NewGuid();
        var vet = LinkedVeterinarian(userId);
        var otherVet = new Veterinarian { Id = Guid.NewGuid(), Name = "Dr. Other", Active = true };
        context.Veterinarians.AddRange(vet, otherVet);
        var (appointment, slot) = ConfirmedAppointment(otherVet);
        context.AppointmentSlots.Add(slot);
        context.Appointments.Add(appointment);
        await context.SaveChangesAsync();

        var service = new ExaminationService(context, VetCaller(userId));
        var dto = new CreateExaminationDto
        {
            PetId = "PET-TEST0001",
            VeterinarianId = vet.Id,
            AppointmentId = appointment.Id,
            Symptoms = "Fever",
            ExaminationDate = DateTime.UtcNow
        };

        await Assert.ThrowsAsync<SchedulingConflictException>(() => service.CreateAsync(dto));
        Assert.Equal(AppointmentStatus.Confirmed, appointment.Status);
    }

    [Fact]
    public async Task CreateAsync_AppointmentAlreadyExamined_ThrowsConflict()
    {
        using var context = GetInMemoryDbContext();
        var userId = Guid.NewGuid();
        var vet = LinkedVeterinarian(userId);
        context.Veterinarians.Add(vet);
        var (appointment, slot) = ConfirmedAppointment(vet);
        context.AppointmentSlots.Add(slot);
        context.Appointments.Add(appointment);
        context.Examinations.Add(new Examination
        {
            Id = Guid.NewGuid(),
            PetId = "PET-TEST0001",
            VeterinarianId = vet.Id,
            AppointmentId = appointment.Id,
            Symptoms = "Earlier",
            ExaminationDate = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = new ExaminationService(context, VetCaller(userId));
        var dto = new CreateExaminationDto
        {
            PetId = "PET-TEST0001",
            VeterinarianId = vet.Id,
            AppointmentId = appointment.Id,
            Symptoms = "Again",
            ExaminationDate = DateTime.UtcNow
        };

        await Assert.ThrowsAsync<SchedulingConflictException>(() => service.CreateAsync(dto));
    }

    [Fact]
    public async Task CreateAsync_UnlinkedVeterinarianUser_ThrowsForbidden()
    {
        using var context = GetInMemoryDbContext();
        var service = new ExaminationService(context, VetCaller(Guid.NewGuid()));
        var dto = new CreateExaminationDto
        {
            PetId = "PET-TEST0001",
            VeterinarianId = Guid.NewGuid(),
            Symptoms = "Fever",
            ExaminationDate = DateTime.UtcNow
        };

        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(dto));
        Assert.Empty(context.Examinations);
    }

    [Fact]
    public async Task CreateAsync_WithoutAppointment_StillCreatesStandaloneExamination()
    {
        using var context = GetInMemoryDbContext();
        var vet = new Veterinarian { Id = Guid.NewGuid(), Name = "Dr. Vet", Active = true };
        context.Veterinarians.Add(vet);
        await context.SaveChangesAsync();

        var service = new ExaminationService(context, TestTenantContext.Unscoped);
        var dto = new CreateExaminationDto
        {
            PetId = "PET-TEST0001",
            VeterinarianId = vet.Id,
            VeterinarianCharge = 1500m,
            Symptoms = "Fever",
            ExaminationDate = DateTime.UtcNow
        };

        var result = await service.CreateAsync(dto);

        Assert.NotNull(result);
        Assert.Null(result.AppointmentId);
        Assert.Equal(1500m, result.VeterinarianCharge);
    }
}
