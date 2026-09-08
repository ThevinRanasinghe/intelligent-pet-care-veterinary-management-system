using Microsoft.EntityFrameworkCore;
using PetCare.Application.DTOs;
using PetCare.Application.Services;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;
using PetCare.Domain.Exceptions;
using PetCare.Infrastructure.Data;
using PetCare.Infrastructure.Services;
using Xunit;

namespace PetCare.Tests;

public class ConsultationServiceTests
{
    private PetCareDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<PetCareDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        return new PetCareDbContext(options);
    }

    private async Task SeedPetAndOwner(PetCareDbContext context, string petId, string ownerId)
    {
        var owner = new PetOwner
        {
            Id = ownerId,
            FullName = "John Doe",
            Email = "john@example.com",
            PhoneNumber = "555-1234"
        };

        var pet = new Pet
        {
            Id = petId,
            OwnerId = ownerId,
            Name = "Cooper",
            Species = "Dog"
        };

        context.PetOwners.Add(owner);
        context.Pets.Add(pet);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task UC09_to_UC13_SubmitConsultation_GeneratesShortId_AndStatusSubmitted()
    {
        // Arrange
        var context = CreateDbContext(nameof(UC09_to_UC13_SubmitConsultation_GeneratesShortId_AndStatusSubmitted));
        await SeedPetAndOwner(context, "PET-1001", "OWN-2001");
        var idGenerator = new SequentialIdGenerator(context);
        var service = new ConsultationService(context, idGenerator);

        var dto = new CreateConsultationRequestDto
        {
            PetId = "PET-1001",
            OwnerId = "OWN-2001",
            SymptomsDescription = "Severe coughing and lethargy after walks.",
            PhotoUrl = "https://example.com/symptom.jpg",
            PreferredDate = DateTime.UtcNow.AddDays(2),
            BudgetLimit = 200.00m,
            PreferredClinicLocationLat = 37.7749,
            PreferredClinicLocationLong = -122.4194,
            PreferredBranch = "West Branch"
        };

        // Act
        var result = await service.SubmitConsultationRequestAsync(dto);

        // Assert
        Assert.NotNull(result);
        Assert.StartsWith("REQ-", result.Id);
        Assert.Equal("PET-1001", result.PetId);
        Assert.Equal("OWN-2001", result.OwnerId);
        Assert.Equal("Submitted", result.Status);
        Assert.Equal(200.00m, result.BudgetLimit);
        Assert.Equal(37.7749, result.PreferredClinicLocationLat);
        Assert.Equal(-122.4194, result.PreferredClinicLocationLong);
        Assert.Equal("West Branch", result.PreferredBranch);

        // Verify history entry created (UC-17)
        var history = await service.GetConsultationHistoryAsync(result.Id);
        Assert.Single(history);
        Assert.Equal("Submitted", history.First().Status);
    }

    [Fact]
    public async Task UC14_SubmitConsultation_WhenPetBelongsToAnotherOwner_ThrowsOwnershipValidationException()
    {
        // Arrange
        var context = CreateDbContext(nameof(UC14_SubmitConsultation_WhenPetBelongsToAnotherOwner_ThrowsOwnershipValidationException));
        await SeedPetAndOwner(context, "PET-1001", "OWN-2001"); // Pet belongs to OWN-2001
        var idGenerator = new SequentialIdGenerator(context);
        var service = new ConsultationService(context, idGenerator);

        var dto = new CreateConsultationRequestDto
        {
            PetId = "PET-1001",
            OwnerId = "OWN-9999", // Requesting owner is OWN-9999 (impostor)
            SymptomsDescription = "Limping",
            BudgetLimit = 100.00m
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<OwnershipValidationException>(() =>
            service.SubmitConsultationRequestAsync(dto));

        Assert.Contains("Pet ownership validation failed", ex.Message);
        Assert.Contains("PET-1001", ex.Message);
        Assert.Contains("OWN-9999", ex.Message);
    }

    [Fact]
    public async Task UC14_ValidatePetOwnershipAsync_ReturnsTrueIfOwned_FalseOtherwise()
    {
        // Arrange
        var context = CreateDbContext(nameof(UC14_ValidatePetOwnershipAsync_ReturnsTrueIfOwned_FalseOtherwise));
        await SeedPetAndOwner(context, "PET-1001", "OWN-2001");
        var idGenerator = new SequentialIdGenerator(context);
        var service = new ConsultationService(context, idGenerator);

        // Act
        var isOwner = await service.ValidatePetOwnershipAsync("PET-1001", "OWN-2001");
        var isNotOwner = await service.ValidatePetOwnershipAsync("PET-1001", "OWN-9999");

        // Assert
        Assert.True(isOwner);
        Assert.False(isNotOwner);
    }

    [Fact]
    public async Task UC15_GetConsultations_ByOwner_And_ByPet()
    {
        // Arrange
        var context = CreateDbContext(nameof(UC15_GetConsultations_ByOwner_And_ByPet));
        await SeedPetAndOwner(context, "PET-1001", "OWN-2001");
        var idGenerator = new SequentialIdGenerator(context);
        var service = new ConsultationService(context, idGenerator);

        await service.SubmitConsultationRequestAsync(new CreateConsultationRequestDto
        {
            PetId = "PET-1001",
            OwnerId = "OWN-2001",
            SymptomsDescription = "Vomiting",
            BudgetLimit = 150m
        });

        // Act
        var byOwner = await service.GetConsultationsByOwnerAsync("OWN-2001");
        var byPet = await service.GetConsultationsByPetAsync("PET-1001");

        // Assert
        Assert.Single(byOwner);
        Assert.Single(byPet);
    }

    [Fact]
    public async Task UC16_TrackConsultationStatus_And_TransitionThroughAllStatuses()
    {
        // Arrange
        var context = CreateDbContext(nameof(UC16_TrackConsultationStatus_And_TransitionThroughAllStatuses));
        await SeedPetAndOwner(context, "PET-1001", "OWN-2001");
        var idGenerator = new SequentialIdGenerator(context);
        var service = new ConsultationService(context, idGenerator);

        var created = await service.SubmitConsultationRequestAsync(new CreateConsultationRequestDto
        {
            PetId = "PET-1001",
            OwnerId = "OWN-2001",
            SymptomsDescription = "Eye discharge",
            BudgetLimit = 120m
        });

        // Test all required enum statuses from UC-16
        var statusesToTest = new[]
        {
            ConsultationStatus.Processing,
            ConsultationStatus.PendingApproval,
            ConsultationStatus.Approved,
            ConsultationStatus.RevisionRequired,
            ConsultationStatus.AppointmentConfirmed,
            ConsultationStatus.Rejected
        };

        foreach (var status in statusesToTest)
        {
            var updated = await service.UpdateConsultationStatusAsync(created.Id, new UpdateConsultationStatusDto
            {
                Status = status,
                Comments = $"Transitioned to {status}"
            });

            Assert.Equal(status.ToString(), updated.Status);
        }

        // Act: Track status
        var tracking = await service.GetConsultationStatusAsync(created.Id);

        // Assert
        Assert.Equal("Rejected", tracking.Status); // Last status set
        Assert.Equal(7, tracking.History.Count); // 1 initial Submitted + 6 transitions
    }

    [Fact]
    public async Task UC17_GetConsultationHistory_ReturnsAuditTrailInDescendingOrder()
    {
        // Arrange
        var context = CreateDbContext(nameof(UC17_GetConsultationHistory_ReturnsAuditTrailInDescendingOrder));
        await SeedPetAndOwner(context, "PET-1001", "OWN-2001");
        var idGenerator = new SequentialIdGenerator(context);
        var service = new ConsultationService(context, idGenerator);

        var created = await service.SubmitConsultationRequestAsync(new CreateConsultationRequestDto
        {
            PetId = "PET-1001",
            OwnerId = "OWN-2001",
            SymptomsDescription = "Loss of appetite",
            BudgetLimit = 80m
        });

        await service.UpdateConsultationStatusAsync(created.Id, new UpdateConsultationStatusDto
        {
            Status = ConsultationStatus.Processing,
            Comments = "Veterinarian reviewing details"
        });

        // Act
        var history = (await service.GetConsultationHistoryAsync(created.Id)).ToList();

        // Assert
        Assert.Equal(2, history.Count);
        Assert.Equal("Processing", history[0].Status);
        Assert.Equal("Submitted", history[1].Status);
    }
}
