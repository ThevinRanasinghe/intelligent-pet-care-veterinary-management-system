using Microsoft.EntityFrameworkCore;
using PetCare.Application.DTOs;
using PetCare.Domain.Entities;
using PetCare.Infrastructure;
using PetCare.Infrastructure.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace PetCare.Tests;

public class DiagnosisServiceTests
{
    private PetCareDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<PetCareDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        
        return new PetCareDbContext(options);
    }

    [Fact]
    public async Task CreateAsync_ValidDiagnosis_ReturnsCreatedDto()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new DiagnosisService(context, TestTenantContext.Unscoped);
        var examinationId = Guid.NewGuid();
        context.Examinations.Add(new Examination { Id = examinationId, PetId = "PET-1", VeterinarianId = Guid.NewGuid() });
        await context.SaveChangesAsync();

        var dto = new CreateDiagnosisDto
        {
            ExaminationId = examinationId,
            ConditionName = "Flu",
            Description = "Mild flu",
            Severity = "Low"
        };

        // Act
        var result = await service.CreateAsync(dto);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(dto.ConditionName, result.ConditionName);
        Assert.Equal(dto.Severity, result.Severity);
        
        var inDb = await context.Diagnoses.FindAsync(result.Id);
        Assert.NotNull(inDb);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsDto()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new DiagnosisService(context, TestTenantContext.Unscoped);
        
        var diagnosis = new Diagnosis
        {
            Id = Guid.NewGuid(),
            ExaminationId = Guid.NewGuid(),
            ConditionName = "Flu",
            Severity = DiagnosisSeverity.Low,
            CreatedAt = DateTime.UtcNow
        };
        context.Diagnoses.Add(diagnosis);
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetByIdAsync(diagnosis.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(diagnosis.Id, result.Id);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new DiagnosisService(context, TestTenantContext.Unscoped);

        // Act
        var result = await service.GetByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByExaminationIdAsync_ReturnsOnlyMatchingDiagnoses()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new DiagnosisService(context, TestTenantContext.Unscoped);
        
        var examinationId1 = Guid.NewGuid();
        var examinationId2 = Guid.NewGuid();

        context.Diagnoses.AddRange(
            new Diagnosis { Id = Guid.NewGuid(), ExaminationId = examinationId1, ConditionName = "C1", Severity = DiagnosisSeverity.Low },
            new Diagnosis { Id = Guid.NewGuid(), ExaminationId = examinationId1, ConditionName = "C2", Severity = DiagnosisSeverity.Moderate },
            new Diagnosis { Id = Guid.NewGuid(), ExaminationId = examinationId2, ConditionName = "C3", Severity = DiagnosisSeverity.High }
        );
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetByExaminationIdAsync(examinationId1);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(examinationId1, r.ExaminationId));
    }

    [Fact]
    public async Task UpdateAsync_ExistingId_UpdatesAndReturnsDto()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new DiagnosisService(context, TestTenantContext.Unscoped);
        
        var diagnosis = new Diagnosis
        {
            Id = Guid.NewGuid(),
            ExaminationId = Guid.NewGuid(),
            ConditionName = "Old Name",
            Severity = DiagnosisSeverity.Low
        };
        context.Diagnoses.Add(diagnosis);
        await context.SaveChangesAsync();

        var updateDto = new UpdateDiagnosisDto
        {
            ConditionName = "New Name",
            Description = "New Desc",
            Severity = "High"
        };

        // Act
        var result = await service.UpdateAsync(diagnosis.Id, updateDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("New Name", result.ConditionName);
        Assert.Equal("High", result.Severity);
        
        var inDb = await context.Diagnoses.FindAsync(diagnosis.Id);
        Assert.Equal("New Name", inDb!.ConditionName);
        Assert.Equal(DiagnosisSeverity.High, inDb.Severity);
    }

    [Fact]
    public async Task UpdateAsync_NonExistentId_ReturnsNull()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new DiagnosisService(context, TestTenantContext.Unscoped);
        
        var updateDto = new UpdateDiagnosisDto { ConditionName = "C", Severity = "Low" };

        // Act
        var result = await service.UpdateAsync(Guid.NewGuid(), updateDto);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_ExistingId_RemovesAndReturnsTrue()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new DiagnosisService(context, TestTenantContext.Unscoped);
        
        var diagnosis = new Diagnosis { Id = Guid.NewGuid(), ExaminationId = Guid.NewGuid(), ConditionName = "C", Severity = DiagnosisSeverity.Low };
        context.Diagnoses.Add(diagnosis);
        await context.SaveChangesAsync();

        // Act
        var result = await service.DeleteAsync(diagnosis.Id);

        // Assert
        Assert.True(result);
        Assert.Empty(context.Diagnoses);
    }

    [Fact]
    public async Task DeleteAsync_NonExistentId_ReturnsFalse()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new DiagnosisService(context, TestTenantContext.Unscoped);

        // Act
        var result = await service.DeleteAsync(Guid.NewGuid());

        // Assert
        Assert.False(result);
    }

    // Edge case: Verify invalid severity string throws ArgumentException
    [Fact]
    public async Task CreateAsync_InvalidSeverity_ThrowsArgumentException()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new DiagnosisService(context, TestTenantContext.Unscoped);
        var examinationId = Guid.NewGuid();
        context.Examinations.Add(new Examination { Id = examinationId, PetId = "PET-1", VeterinarianId = Guid.NewGuid() });
        await context.SaveChangesAsync();

        var dto = new CreateDiagnosisDto
        {
            ExaminationId = examinationId,
            ConditionName = "Flu",
            Severity = "InvalidSeverityString"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(dto));
    }
}
