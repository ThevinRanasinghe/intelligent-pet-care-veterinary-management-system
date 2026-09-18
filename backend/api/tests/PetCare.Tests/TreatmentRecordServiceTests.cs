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

public class TreatmentRecordServiceTests
{
    private AppDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        
        return new AppDbContext(options);
    }

    [Fact]
    public async Task CreateAsync_ValidTreatmentRecord_ReturnsCreatedDto()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new TreatmentRecordService(context);
        var dto = new CreateTreatmentRecordDto
        {
            DiagnosisId = Guid.NewGuid(),
            ProcedureName = "Surgery",
            Notes = "Routine"
        };

        // Act
        var result = await service.CreateAsync(dto);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(dto.ProcedureName, result.ProcedureName);
        Assert.Equal("Planned", result.Status); // Defaults to Planned
        
        var inDb = await context.TreatmentRecords.FindAsync(result.Id);
        Assert.NotNull(inDb);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsDto()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new TreatmentRecordService(context);
        
        var record = new TreatmentRecord
        {
            Id = Guid.NewGuid(),
            DiagnosisId = Guid.NewGuid(),
            ProcedureName = "Surgery",
            Status = TreatmentStatus.Planned,
            CreatedAt = DateTime.UtcNow
        };
        context.TreatmentRecords.Add(record);
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetByIdAsync(record.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(record.Id, result.Id);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new TreatmentRecordService(context);

        // Act
        var result = await service.GetByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByDiagnosisIdAsync_ReturnsOnlyMatchingRecords()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new TreatmentRecordService(context);
        
        var diagnosisId1 = Guid.NewGuid();
        var diagnosisId2 = Guid.NewGuid();

        context.TreatmentRecords.AddRange(
            new TreatmentRecord { Id = Guid.NewGuid(), DiagnosisId = diagnosisId1, ProcedureName = "P1", Status = TreatmentStatus.Planned },
            new TreatmentRecord { Id = Guid.NewGuid(), DiagnosisId = diagnosisId1, ProcedureName = "P2", Status = TreatmentStatus.InProgress },
            new TreatmentRecord { Id = Guid.NewGuid(), DiagnosisId = diagnosisId2, ProcedureName = "P3", Status = TreatmentStatus.Completed }
        );
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetByDiagnosisIdAsync(diagnosisId1);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(diagnosisId1, r.DiagnosisId));
    }

    [Fact]
    public async Task UpdateAsync_ExistingId_UpdatesAndReturnsDto()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new TreatmentRecordService(context);
        
        var record = new TreatmentRecord
        {
            Id = Guid.NewGuid(),
            DiagnosisId = Guid.NewGuid(),
            ProcedureName = "Old Name",
            Status = TreatmentStatus.Planned
        };
        context.TreatmentRecords.Add(record);
        await context.SaveChangesAsync();

        var updateDto = new UpdateTreatmentRecordDto
        {
            ProcedureName = "New Name",
            Notes = "New Notes"
        };

        // Act
        var result = await service.UpdateAsync(record.Id, updateDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("New Name", result.ProcedureName);
        
        var inDb = await context.TreatmentRecords.FindAsync(record.Id);
        Assert.Equal("New Name", inDb!.ProcedureName);
    }

    [Fact]
    public async Task UpdateAsync_NonExistentId_ReturnsNull()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new TreatmentRecordService(context);
        
        var updateDto = new UpdateTreatmentRecordDto { ProcedureName = "P" };

        // Act
        var result = await service.UpdateAsync(Guid.NewGuid(), updateDto);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateStatusAsync_ExistingId_UpdatesStatus()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new TreatmentRecordService(context);
        
        var record = new TreatmentRecord
        {
            Id = Guid.NewGuid(),
            DiagnosisId = Guid.NewGuid(),
            ProcedureName = "P",
            Status = TreatmentStatus.Planned
        };
        context.TreatmentRecords.Add(record);
        await context.SaveChangesAsync();

        var updateStatusDto = new UpdateTreatmentStatusDto { Status = "Completed" };

        // Act
        var result = await service.UpdateStatusAsync(record.Id, updateStatusDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Completed", result.Status);
        
        var inDb = await context.TreatmentRecords.FindAsync(record.Id);
        Assert.Equal(TreatmentStatus.Completed, inDb!.Status);
    }

    [Fact]
    public async Task DeleteAsync_ExistingId_RemovesAndReturnsTrue()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new TreatmentRecordService(context);
        
        var record = new TreatmentRecord { Id = Guid.NewGuid(), DiagnosisId = Guid.NewGuid(), ProcedureName = "P", Status = TreatmentStatus.Planned };
        context.TreatmentRecords.Add(record);
        await context.SaveChangesAsync();

        // Act
        var result = await service.DeleteAsync(record.Id);

        // Assert
        Assert.True(result);
        Assert.Empty(context.TreatmentRecords);
    }

    [Fact]
    public async Task DeleteAsync_NonExistentId_ReturnsFalse()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new TreatmentRecordService(context);

        // Act
        var result = await service.DeleteAsync(Guid.NewGuid());

        // Assert
        Assert.False(result);
    }

    // Edge case: Verify invalid status string throws ArgumentException
    [Fact]
    public async Task UpdateStatusAsync_InvalidStatus_ThrowsArgumentException()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new TreatmentRecordService(context);
        
        var record = new TreatmentRecord { Id = Guid.NewGuid(), DiagnosisId = Guid.NewGuid(), ProcedureName = "P", Status = TreatmentStatus.Planned };
        context.TreatmentRecords.Add(record);
        await context.SaveChangesAsync();

        var dto = new UpdateTreatmentStatusDto { Status = "InvalidStatus" };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateStatusAsync(record.Id, dto));
    }
}
