using Microsoft.EntityFrameworkCore;
using PetCare.Application.DTOs;
using PetCare.Application.Services;
using PetCare.Domain.Entities;
using PetCare.Domain.Exceptions;
using PetCare.Infrastructure.Data;
using PetCare.Infrastructure.Services;
using Xunit;

namespace PetCare.Tests;

public class PetServiceTests
{
    private PetCareDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<PetCareDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new PetCareDbContext(options);
    }

    [Fact]
    public async Task UC05_RegisterPet_GeneratesShortId_AndStoresPet()
    {
        // Arrange
        var context = CreateDbContext(nameof(UC05_RegisterPet_GeneratesShortId_AndStoresPet));
        var idGenerator = new SequentialIdGenerator(context);
        var service = new PetService(context, idGenerator);

        var dto = new CreatePetDto
        {
            OwnerId = "OWN-2001",
            Name = "Max",
            Species = "Dog",
            Breed = "Labrador",
            Age = 4,
            Notes = "Friendly and healthy"
        };

        // Act
        var result = await service.RegisterPetAsync(dto);

        // Assert
        Assert.NotNull(result);
        Assert.StartsWith("PET-", result.Id);
        Assert.Equal("Max", result.Name);
        Assert.Equal("OWN-2001", result.OwnerId);

        var stored = await context.Pets.FindAsync(result.Id);
        Assert.NotNull(stored);
        Assert.Equal("Max", stored.Name);
    }

    [Fact]
    public async Task UC05_RegisterPet_WithCustomShortId_UsesSpecifiedId()
    {
        // Arrange
        var context = CreateDbContext(nameof(UC05_RegisterPet_WithCustomShortId_UsesSpecifiedId));
        var idGenerator = new SequentialIdGenerator(context);
        var service = new PetService(context, idGenerator);

        var dto = new CreatePetDto
        {
            Id = "PET-9999",
            OwnerId = "OWN-2001",
            Name = "Bella",
            Species = "Cat",
            Breed = "Siamese",
            Age = 2
        };

        // Act
        var result = await service.RegisterPetAsync(dto);

        // Assert
        Assert.Equal("PET-9999", result.Id);
    }

    [Fact]
    public async Task UC06_GetPetById_And_GetPetsByOwner_ReturnProfiles()
    {
        // Arrange
        var context = CreateDbContext(nameof(UC06_GetPetById_And_GetPetsByOwner_ReturnProfiles));
        var idGenerator = new SequentialIdGenerator(context);
        var service = new PetService(context, idGenerator);

        await service.RegisterPetAsync(new CreatePetDto
        {
            Id = "PET-1001",
            OwnerId = "OWN-2001",
            Name = "Rocky",
            Species = "Dog"
        });

        // Act
        var pet = await service.GetPetByIdAsync("PET-1001");
        var ownerPets = await service.GetPetsByOwnerAsync("OWN-2001");

        // Assert
        Assert.NotNull(pet);
        Assert.Equal("Rocky", pet.Name);
        Assert.Single(ownerPets);
    }

    [Fact]
    public async Task UC07_UpdatePetProfile_UpdatesNonClinicalFields()
    {
        // Arrange
        var context = CreateDbContext(nameof(UC07_UpdatePetProfile_UpdatesNonClinicalFields));
        var idGenerator = new SequentialIdGenerator(context);
        var service = new PetService(context, idGenerator);

        await service.RegisterPetAsync(new CreatePetDto
        {
            Id = "PET-1001",
            OwnerId = "OWN-2001",
            Name = "Rocky",
            Species = "Dog",
            Age = 2,
            Notes = "Original notes"
        });

        var updateDto = new UpdatePetProfileDto
        {
            Name = "Rocky Jr.",
            Species = "Dog",
            Breed = "Boxer",
            Age = 3,
            Notes = "Updated personality notes"
        };

        // Act
        var updated = await service.UpdatePetProfileAsync("PET-1001", updateDto, "OWN-2001");

        // Assert
        Assert.Equal("Rocky Jr.", updated.Name);
        Assert.Equal(3, updated.Age);
        Assert.Equal("Updated personality notes", updated.Notes);
    }

    [Fact]
    public async Task UC07_UpdatePetProfile_WhenOwnerMismatch_ThrowsOwnershipValidationException()
    {
        // Arrange
        var context = CreateDbContext(nameof(UC07_UpdatePetProfile_WhenOwnerMismatch_ThrowsOwnershipValidationException));
        var idGenerator = new SequentialIdGenerator(context);
        var service = new PetService(context, idGenerator);

        await service.RegisterPetAsync(new CreatePetDto
        {
            Id = "PET-1001",
            OwnerId = "OWN-2001",
            Name = "Rocky",
            Species = "Dog"
        });

        var updateDto = new UpdatePetProfileDto
        {
            Name = "Hacked Name"
        };

        // Act & Assert
        await Assert.ThrowsAsync<OwnershipValidationException>(() =>
            service.UpdatePetProfileAsync("PET-1001", updateDto, "OWN-9999"));
    }

    [Fact]
    public async Task UC08_ViewPetMedicalAndVaccinationHistory_And_AddClinicalEntries()
    {
        // Arrange
        var context = CreateDbContext(nameof(UC08_ViewPetMedicalAndVaccinationHistory_And_AddClinicalEntries));
        var idGenerator = new SequentialIdGenerator(context);
        var service = new PetService(context, idGenerator);

        await service.RegisterPetAsync(new CreatePetDto
        {
            Id = "PET-1001",
            OwnerId = "OWN-2001",
            Name = "Daisy",
            Species = "Dog"
        });

        // Add Medical Record
        var medRecord = await service.AddMedicalRecordAsync("PET-1001", new CreateMedicalRecordDto
        {
            Diagnosis = "Skin Allergy",
            Treatment = "Antihistamines 10mg daily",
            VeterinarianName = "Dr. Smith",
            ClinicalNotes = "Check back in 2 weeks"
        });

        // Add Vaccination Record
        var vacRecord = await service.AddVaccinationRecordAsync("PET-1001", new CreateVaccinationRecordDto
        {
            VaccineName = "Bordetella",
            DateAdministered = DateTime.UtcNow,
            NextDueDate = DateTime.UtcNow.AddYears(1),
            VeterinarianName = "Dr. Smith"
        });

        // Act
        var history = await service.GetPetMedicalHistoryAsync("PET-1001");

        // Assert
        Assert.NotNull(history);
        Assert.StartsWith("MED-", medRecord.Id);
        Assert.StartsWith("VAC-", vacRecord.Id);
        Assert.Single(history.MedicalRecords);
        Assert.Equal("Skin Allergy", history.MedicalRecords[0].Diagnosis);
        Assert.Single(history.VaccinationRecords);
        Assert.Equal("Bordetella", history.VaccinationRecords[0].VaccineName);
    }
}
