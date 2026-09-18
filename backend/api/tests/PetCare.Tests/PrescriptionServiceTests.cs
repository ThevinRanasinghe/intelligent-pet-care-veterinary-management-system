using Microsoft.EntityFrameworkCore;
using PetCare.Application.DTOs;
using PetCare.Domain.Entities;
using PetCare.Infrastructure.Persistence;
using PetCare.Infrastructure.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace PetCare.Tests;

public class PrescriptionServiceTests
{
    private AppDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        
        return new AppDbContext(options);
    }

    [Fact]
    public async Task CreateAsync_ValidPrescription_ReturnsCreatedDto()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new PrescriptionService(context);
        var dto = new CreatePrescriptionDto
        {
            TreatmentRecordId = Guid.NewGuid(),
            MedicineId = Guid.NewGuid(),
            Dosage = "1 pill daily",
            DurationDays = 7
        };

        // Act
        var result = await service.CreateAsync(dto);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(dto.Dosage, result.Dosage);
        Assert.Equal(dto.DurationDays, result.DurationDays);
        
        var inDb = await context.Prescriptions.FindAsync(result.Id);
        Assert.NotNull(inDb);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsDto()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new PrescriptionService(context);
        
        var prescription = new Prescription
        {
            Id = Guid.NewGuid(),
            TreatmentRecordId = Guid.NewGuid(),
            MedicineId = Guid.NewGuid(),
            Dosage = "1 pill daily",
            DurationDays = 7,
            CreatedAt = DateTime.UtcNow
        };
        context.Prescriptions.Add(prescription);
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetByIdAsync(prescription.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(prescription.Id, result.Id);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new PrescriptionService(context);

        // Act
        var result = await service.GetByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByTreatmentRecordIdAsync_ReturnsOnlyMatchingPrescriptions()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new PrescriptionService(context);
        
        var treatmentRecordId1 = Guid.NewGuid();
        var treatmentRecordId2 = Guid.NewGuid();

        context.Prescriptions.AddRange(
            new Prescription { Id = Guid.NewGuid(), TreatmentRecordId = treatmentRecordId1, MedicineId = Guid.NewGuid(), Dosage = "D1", DurationDays = 1 },
            new Prescription { Id = Guid.NewGuid(), TreatmentRecordId = treatmentRecordId1, MedicineId = Guid.NewGuid(), Dosage = "D2", DurationDays = 2 },
            new Prescription { Id = Guid.NewGuid(), TreatmentRecordId = treatmentRecordId2, MedicineId = Guid.NewGuid(), Dosage = "D3", DurationDays = 3 }
        );
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetByTreatmentRecordIdAsync(treatmentRecordId1);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(treatmentRecordId1, r.TreatmentRecordId));
    }

    [Fact]
    public async Task DeleteAsync_ExistingId_RemovesAndReturnsTrue()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new PrescriptionService(context);
        
        var prescription = new Prescription { Id = Guid.NewGuid(), TreatmentRecordId = Guid.NewGuid(), MedicineId = Guid.NewGuid(), Dosage = "D", DurationDays = 1 };
        context.Prescriptions.Add(prescription);
        await context.SaveChangesAsync();

        // Act
        var result = await service.DeleteAsync(prescription.Id);

        // Assert
        Assert.True(result);
        Assert.Empty(context.Prescriptions);
    }

    [Fact]
    public async Task DeleteAsync_NonExistentId_ReturnsFalse()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new PrescriptionService(context);

        // Act
        var result = await service.DeleteAsync(Guid.NewGuid());

        // Assert
        Assert.False(result);
    }

    // Edge case: Verify GetByTreatmentRecordIdAsync returns empty list when no matches found
    [Fact]
    public async Task GetByTreatmentRecordIdAsync_NoMatchingRecords_ReturnsEmptyList()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new PrescriptionService(context);
        
        var prescription = new Prescription { Id = Guid.NewGuid(), TreatmentRecordId = Guid.NewGuid(), MedicineId = Guid.NewGuid(), Dosage = "D", DurationDays = 1 };
        context.Prescriptions.Add(prescription);
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetByTreatmentRecordIdAsync(Guid.NewGuid());

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }
}
