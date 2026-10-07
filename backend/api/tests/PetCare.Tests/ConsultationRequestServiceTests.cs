using Microsoft.EntityFrameworkCore;
using PetCare.Application.DTOs.Consultations;
using PetCare.Domain.Entities;
using PetCare.Infrastructure.Data;
using PetCare.Infrastructure.Services;
using Xunit;

namespace PetCare.Tests;

/// <summary>
/// Unit and integration tests for ConsultationRequestService.
/// Component: Pet &amp; Consultation Request Management.
/// Tests business validations, ownership verification, state transitions,
/// and audit status history tracking using EF Core InMemory database.
/// </summary>
public class ConsultationRequestServiceTests
{
    private static PetCareDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PetCareDbContext>()
            .UseInMemoryDatabase(databaseName: $"PetCare_Consultation_Test_{Guid.NewGuid():N}")
            .Options;

        return new PetCareDbContext(options);
    }

    private static async Task<(PetOwner Owner, Pet Pet)> SeedOwnerAndPetAsync(
        PetCareDbContext context,
        string ownerId = "OWN-001",
        string petId = "PET-001")
    {
        var owner = new PetOwner
        {
            Id = ownerId,
            FullName = "John Doe",
            Email = "john.doe@example.com",
            PhoneNumber = "+1234567890",
            Address = "123 Pet Street"
        };

        var pet = new Pet
        {
            Id = petId,
            OwnerId = ownerId,
            Name = "Buddy",
            Species = "Dog",
            Breed = "Golden Retriever",
            Age = 3,
            Gender = "Male",
            Weight = 28.5m,
            Notes = "Friendly and active"
        };

        context.PetOwners.Add(owner);
        context.Pets.Add(pet);
        await context.SaveChangesAsync();

        return (owner, pet);
    }

    // ========================================================================
    // 1. Valid Consultation Request Creation
    // ========================================================================
    [Fact]
    public async Task CreateAsync_ValidInput_CreatesConsultationRequestInDraftStatus()
    {
        // Arrange
        using var context = CreateDbContext();
        var (owner, pet) = await SeedOwnerAndPetAsync(context);
        var service = new ConsultationRequestService(context);

        var dto = new CreateConsultationRequestDto
        {
            OwnerId = owner.Id,
            PetId = pet.Id,
            Symptoms = "Pet has a mild cough and decreased appetite for 2 days.",
            SymptomPhotoUrl = "https://storage.example.com/photos/symptom1.jpg",
            Budget = 150.00m,
            PreferredDate = DateTime.UtcNow.AddDays(2),
            Latitude = 40.7128m,
            Longitude = -74.0060m,
            AdditionalNotes = "Prefers morning appointment."
        };

        // Act
        var result = await service.CreateAsync(dto);

        // Assert
        Assert.NotNull(result);
        Assert.StartsWith("CON-", result.Id);
        Assert.Equal(owner.Id, result.OwnerId);
        Assert.Equal(pet.Id, result.PetId);
        Assert.Equal("Buddy", result.PetName);
        Assert.Equal("Draft", result.Status);
        Assert.Equal(dto.Symptoms, result.Symptoms);
        Assert.Equal(150.00m, result.Budget);
        Assert.Equal(dto.SymptomPhotoUrl, result.SymptomPhotoUrl);
        Assert.Equal("Prefers morning appointment.", result.AdditionalNotes);

        var dbEntity = await context.ConsultationRequests.FindAsync(result.Id);
        Assert.NotNull(dbEntity);
        Assert.Equal("Draft", dbEntity.Status);
    }

    // ========================================================================
    // 2. Empty Symptoms Validation
    // ========================================================================
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task CreateAsync_EmptyOrWhitespaceSymptoms_ThrowsArgumentException(string? invalidSymptoms)
    {
        // Arrange
        using var context = CreateDbContext();
        var (owner, pet) = await SeedOwnerAndPetAsync(context);
        var service = new ConsultationRequestService(context);

        var dto = new CreateConsultationRequestDto
        {
            OwnerId = owner.Id,
            PetId = pet.Id,
            Symptoms = invalidSymptoms!,
            Budget = 100m
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(dto));
        Assert.Equal("Symptoms are required.", exception.Message);
    }

    // ========================================================================
    // 3. Negative Budget Validation
    // ========================================================================
    [Fact]
    public async Task CreateAsync_NegativeBudget_ThrowsArgumentException()
    {
        // Arrange
        using var context = CreateDbContext();
        var (owner, pet) = await SeedOwnerAndPetAsync(context);
        var service = new ConsultationRequestService(context);

        var dto = new CreateConsultationRequestDto
        {
            OwnerId = owner.Id,
            PetId = pet.Id,
            Symptoms = "Fever and lethargy.",
            Budget = -50.00m
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(dto));
        Assert.Equal("Budget cannot be negative.", exception.Message);
    }

    // ========================================================================
    // 4. Pet Ownership Validation
    // ========================================================================
    [Fact]
    public async Task CreateAsync_PetDoesNotBelongToOwner_ThrowsArgumentException()
    {
        // Arrange
        using var context = CreateDbContext();
        var (owner1, pet1) = await SeedOwnerAndPetAsync(context, "OWN-001", "PET-001");

        var owner2 = new PetOwner
        {
            Id = "OWN-002",
            FullName = "Jane Smith",
            Email = "jane.smith@example.com",
            PhoneNumber = "+9876543210"
        };
        context.PetOwners.Add(owner2);
        await context.SaveChangesAsync();

        var service = new ConsultationRequestService(context);

        // Owner 2 tries to create consultation for Owner 1's pet
        var dto = new CreateConsultationRequestDto
        {
            OwnerId = owner2.Id,
            PetId = pet1.Id,
            Symptoms = "Limping left hind leg.",
            Budget = 120m
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(dto));
        Assert.Equal("Pet was not found or does not belong to this owner.", exception.Message);

        var isOwnerValid = await service.ValidateOwnershipAsync(pet1.Id, owner2.Id);
        Assert.False(isOwnerValid);

        var isRealOwnerValid = await service.ValidateOwnershipAsync(pet1.Id, owner1.Id);
        Assert.True(isRealOwnerValid);
    }

    // ========================================================================
    // 5. Get Consultation Request by ID
    // ========================================================================
    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsConsultationRequestDtoWithPetDetails()
    {
        // Arrange
        using var context = CreateDbContext();
        var (owner, pet) = await SeedOwnerAndPetAsync(context);
        var service = new ConsultationRequestService(context);

        var created = await service.CreateAsync(new CreateConsultationRequestDto
        {
            OwnerId = owner.Id,
            PetId = pet.Id,
            Symptoms = "Routine dental checkup inquiry.",
            Budget = 80m
        });

        // Act
        var result = await service.GetByIdAsync(created.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
        Assert.Equal(owner.Id, result.OwnerId);
        Assert.Equal(pet.Id, result.PetId);
        Assert.Equal("Buddy", result.PetName);
        Assert.Equal("Routine dental checkup inquiry.", result.Symptoms);
        Assert.Equal("Draft", result.Status);
    }

    // ========================================================================
    // 6. Get Consultations by Owner
    // ========================================================================
    [Fact]
    public async Task GetByOwnerIdAsync_ReturnsOnlyConsultationsBelongingToRequestedOwner()
    {
        // Arrange
        using var context = CreateDbContext();
        var (owner1, pet1) = await SeedOwnerAndPetAsync(context, "OWN-001", "PET-001");
        var (owner2, pet2) = await SeedOwnerAndPetAsync(context, "OWN-002", "PET-002");
        var service = new ConsultationRequestService(context);

        // 2 requests for owner 1
        await service.CreateAsync(new CreateConsultationRequestDto
        {
            OwnerId = owner1.Id,
            PetId = pet1.Id,
            Symptoms = "Owner 1 Request A",
            Budget = 50m
        });
        await service.CreateAsync(new CreateConsultationRequestDto
        {
            OwnerId = owner1.Id,
            PetId = pet1.Id,
            Symptoms = "Owner 1 Request B",
            Budget = 75m
        });

        // 1 request for owner 2
        await service.CreateAsync(new CreateConsultationRequestDto
        {
            OwnerId = owner2.Id,
            PetId = pet2.Id,
            Symptoms = "Owner 2 Request C",
            Budget = 100m
        });

        // Act
        var owner1Consultations = await service.GetByOwnerIdAsync(owner1.Id);
        var owner2Consultations = await service.GetByOwnerIdAsync(owner2.Id);

        // Assert
        Assert.Equal(2, owner1Consultations.Count);
        Assert.All(owner1Consultations, c => Assert.Equal(owner1.Id, c.OwnerId));

        Assert.Single(owner2Consultations);
        Assert.Equal(owner2.Id, owner2Consultations[0].OwnerId);
    }

    // ========================================================================
    // 7. Update Draft Consultation Request
    // ========================================================================
    [Fact]
    public async Task UpdateAsync_DraftStatus_SuccessfullyUpdatesFields()
    {
        // Arrange
        using var context = CreateDbContext();
        var (owner, pet) = await SeedOwnerAndPetAsync(context);
        var service = new ConsultationRequestService(context);

        var created = await service.CreateAsync(new CreateConsultationRequestDto
        {
            OwnerId = owner.Id,
            PetId = pet.Id,
            Symptoms = "Initial symptoms: vomiting.",
            Budget = 100m,
            AdditionalNotes = "Initial note"
        });

        var updateDto = new UpdateConsultationRequestDto
        {
            Symptoms = "Updated symptoms: vomiting and lethargy after eating grass.",
            Budget = 200m,
            SymptomPhotoUrl = "https://storage.example.com/photos/updated.jpg",
            Latitude = 40.7500m,
            Longitude = -73.9900m,
            AdditionalNotes = "Updated note: Urgent review requested."
        };

        // Act
        var updated = await service.UpdateAsync(created.Id, updateDto);

        // Assert
        Assert.NotNull(updated);
        Assert.Equal("Updated symptoms: vomiting and lethargy after eating grass.", updated.Symptoms);
        Assert.Equal(200m, updated.Budget);
        Assert.Equal("https://storage.example.com/photos/updated.jpg", updated.SymptomPhotoUrl);
        Assert.Equal("Updated note: Urgent review requested.", updated.AdditionalNotes);
        Assert.Equal("Draft", updated.Status);
    }

    // ========================================================================
    // 8. Submit Draft Consultation Request
    // ========================================================================
    [Fact]
    public async Task SubmitAsync_DraftConsultation_TransitionsStatusToSubmitted()
    {
        // Arrange
        using var context = CreateDbContext();
        var (owner, pet) = await SeedOwnerAndPetAsync(context);
        var service = new ConsultationRequestService(context);

        var created = await service.CreateAsync(new CreateConsultationRequestDto
        {
            OwnerId = owner.Id,
            PetId = pet.Id,
            Symptoms = "Ear irritation and frequent head shaking.",
            Budget = 110m
        });

        // Act
        var submitted = await service.SubmitAsync(created.Id);

        // Assert
        Assert.NotNull(submitted);
        Assert.Equal("Submitted", submitted.Status);
        Assert.Equal("Consultation request submitted by pet owner.", submitted.AdditionalNotes);

        var dbEntity = await context.ConsultationRequests.FindAsync(created.Id);
        Assert.NotNull(dbEntity);
        Assert.Equal("Submitted", dbEntity.Status);
    }

    // ========================================================================
    // 9. Submit Creates Status History
    // ========================================================================
    [Fact]
    public async Task SubmitAsync_DraftConsultation_RecordsStatusHistoryEntry()
    {
        // Arrange
        using var context = CreateDbContext();
        var (owner, pet) = await SeedOwnerAndPetAsync(context);
        var service = new ConsultationRequestService(context);

        var created = await service.CreateAsync(new CreateConsultationRequestDto
        {
            OwnerId = owner.Id,
            PetId = pet.Id,
            Symptoms = "Skin rash and scratching.",
            Budget = 95m
        });

        // Act
        await service.SubmitAsync(created.Id);

        // Assert
        var history = await service.GetStatusHistoryAsync(created.Id);
        Assert.NotEmpty(history);
        Assert.Contains(history, h => h.Status == "Submitted" && h.Comments == "Consultation request submitted by pet owner.");
    }

    // ========================================================================
    // 10. Cancel Draft Consultation Request and Creates Status History
    // ========================================================================
    [Fact]
    public async Task CancelAsync_DraftConsultation_CancelsRequestAndRecordsStatusHistory()
    {
        // Arrange
        using var context = CreateDbContext();
        var (owner, pet) = await SeedOwnerAndPetAsync(context);
        var service = new ConsultationRequestService(context);

        var created = await service.CreateAsync(new CreateConsultationRequestDto
        {
            OwnerId = owner.Id,
            PetId = pet.Id,
            Symptoms = "Mild eye discharge.",
            Budget = 60m
        });

        // Act
        var cancelSuccess = await service.CancelAsync(created.Id);

        // Assert
        Assert.True(cancelSuccess);

        var dbEntity = await context.ConsultationRequests.FindAsync(created.Id);
        Assert.NotNull(dbEntity);
        Assert.Equal("Cancelled", dbEntity.Status);
        Assert.Equal("Cancelled by pet owner.", dbEntity.StatusNotes);

        var history = await service.GetStatusHistoryAsync(created.Id);
        Assert.NotEmpty(history);
        Assert.Contains(history, h => h.Status == "Cancelled" && h.Comments == "Status changed from Draft to Cancelled.");
    }
}
