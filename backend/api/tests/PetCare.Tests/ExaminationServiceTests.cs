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

public class ExaminationServiceTests
{
    private AppDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        
        return new AppDbContext(options);
    }

    [Fact]
    public async Task CreateAsync_ValidExamination_ReturnsCreatedDto()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new ExaminationService(context);
        var dto = new CreateExaminationDto
        {
            PetId = Guid.NewGuid(),
            VeterinarianId = Guid.NewGuid(),
            Symptoms = "Fever",
            Notes = "Checkup required",
            ExaminationDate = DateTime.UtcNow
        };

        // Act
        var result = await service.CreateAsync(dto);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(dto.Symptoms, result.Symptoms);
        
        var inDb = await context.Examinations.FindAsync(result.Id);
        Assert.NotNull(inDb);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsDto()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new ExaminationService(context);
        
        var examination = new Examination
        {
            Id = Guid.NewGuid(),
            PetId = Guid.NewGuid(),
            VeterinarianId = Guid.NewGuid(),
            Symptoms = "Cough",
            ExaminationDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        context.Examinations.Add(examination);
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetByIdAsync(examination.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(examination.Id, result.Id);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new ExaminationService(context);

        // Act
        var result = await service.GetByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByPetIdAsync_ReturnsOnlyMatchingExaminations()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new ExaminationService(context);
        
        var petId1 = Guid.NewGuid();
        var petId2 = Guid.NewGuid();

        context.Examinations.AddRange(
            new Examination { Id = Guid.NewGuid(), PetId = petId1, VeterinarianId = Guid.NewGuid(), Symptoms = "S1", ExaminationDate = DateTime.UtcNow },
            new Examination { Id = Guid.NewGuid(), PetId = petId1, VeterinarianId = Guid.NewGuid(), Symptoms = "S2", ExaminationDate = DateTime.UtcNow },
            new Examination { Id = Guid.NewGuid(), PetId = petId2, VeterinarianId = Guid.NewGuid(), Symptoms = "S3", ExaminationDate = DateTime.UtcNow }
        );
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetByPetIdAsync(petId1);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal(petId1, r.PetId));
    }

    [Fact]
    public async Task UpdateAsync_ExistingId_UpdatesAndReturnsDto()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new ExaminationService(context);
        
        var examination = new Examination
        {
            Id = Guid.NewGuid(),
            PetId = Guid.NewGuid(),
            VeterinarianId = Guid.NewGuid(),
            Symptoms = "Old Symptoms",
            ExaminationDate = DateTime.UtcNow
        };
        context.Examinations.Add(examination);
        await context.SaveChangesAsync();

        var updateDto = new UpdateExaminationDto
        {
            Symptoms = "New Symptoms",
            Notes = "New Notes",
            ExaminationDate = DateTime.UtcNow.AddDays(1)
        };

        // Act
        var result = await service.UpdateAsync(examination.Id, updateDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("New Symptoms", result.Symptoms);
        
        var inDb = await context.Examinations.FindAsync(examination.Id);
        Assert.Equal("New Symptoms", inDb!.Symptoms);
    }

    [Fact]
    public async Task UpdateAsync_NonExistentId_ReturnsNull()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new ExaminationService(context);
        
        var updateDto = new UpdateExaminationDto { Symptoms = "S" };

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
        var service = new ExaminationService(context);
        
        var examination = new Examination { Id = Guid.NewGuid(), PetId = Guid.NewGuid(), VeterinarianId = Guid.NewGuid(), Symptoms = "S" };
        context.Examinations.Add(examination);
        await context.SaveChangesAsync();

        // Act
        var result = await service.DeleteAsync(examination.Id);

        // Assert
        Assert.True(result);
        Assert.Empty(context.Examinations);
    }

    [Fact]
    public async Task DeleteAsync_NonExistentId_ReturnsFalse()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new ExaminationService(context);

        // Act
        var result = await service.DeleteAsync(Guid.NewGuid());

        // Assert
        Assert.False(result);
    }

    // Edge case: Verify CreateAsync handles empty strings for optional Notes
    [Fact]
    public async Task CreateAsync_EmptyNotes_Succeeds()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new ExaminationService(context);
        var dto = new CreateExaminationDto
        {
            PetId = Guid.NewGuid(),
            VeterinarianId = Guid.NewGuid(),
            Symptoms = "Fever",
            Notes = "", // Empty string
            ExaminationDate = DateTime.UtcNow
        };

        // Act
        var result = await service.CreateAsync(dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("", result.Notes);
    }
}
