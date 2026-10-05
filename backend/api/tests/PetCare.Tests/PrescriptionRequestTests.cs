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
/// InMemory-backed tests for the medicine-request fields on prescriptions:
/// new prescriptions start Pending with the requested quantity, and
/// veterinarians can only prescribe on their own examinations.
/// </summary>
public class PrescriptionRequestTests
{
    private static PetCareDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<PetCareDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PetCareDbContext(options);
    }

    private static async Task<TreatmentRecord> SeedClinicalGraphAsync(
        PetCareDbContext context, Veterinarian vet)
    {
        var owner = new PetOwner { Id = "OWN-TEST0001", FullName = "Test Owner", Email = "owner@example.com" };
        var pet = new Pet { Id = "PET-TEST0001", OwnerId = owner.Id, Name = "Shadow", Species = "Dog", Breed = "Mixed" };
        var examination = new Examination
        {
            Id = Guid.NewGuid(),
            PetId = "PET-TEST0001",
            VeterinarianId = vet.Id,
            Symptoms = "S",
            ExaminationDate = DateTime.UtcNow
        };
        var diagnosis = new Diagnosis
        {
            Id = Guid.NewGuid(),
            ExaminationId = examination.Id,
            ConditionName = "Infection"
        };
        var treatmentRecord = new TreatmentRecord
        {
            Id = Guid.NewGuid(),
            DiagnosisId = diagnosis.Id,
            ProcedureName = "Medication"
        };
        context.PetOwners.Add(owner);
        context.Pets.Add(pet);
        context.Examinations.Add(examination);
        context.Diagnoses.Add(diagnosis);
        context.TreatmentRecords.Add(treatmentRecord);
        await context.SaveChangesAsync();
        return treatmentRecord;
    }

    [Fact]
    public async Task CreateAsync_NewPrescription_StartsPendingWithQuantity()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var vet = new Veterinarian { Id = Guid.NewGuid(), Name = "Dr. Vet", Active = true };
        context.Veterinarians.Add(vet);
        var medicineId = Guid.NewGuid();
        context.Medicines.Add(new Medicine { Id = medicineId, Name = "Amoxicillin", UnitPrice = 100m });
        var treatmentRecord = await SeedClinicalGraphAsync(context, vet);

        var service = new PrescriptionService(context, TestTenantContext.Unscoped);
        var dto = new CreatePrescriptionDto
        {
            TreatmentRecordId = treatmentRecord.Id,
            Items = { new CreatePrescriptionItemDto
            {
                MedicineId = medicineId,
                Dosage = "1 pill twice daily",
                DurationDays = 7,
                Quantity = 14,
                Frequency = "twice daily",
                Instructions = "Give with food"
            } }
        };

        // Act
        var result = await service.CreateAsync(dto);

        // Assert
        var created = Assert.Single(result);
        Assert.Equal("Pending", created.RequestStatus);
        Assert.Equal(14, created.Quantity);
        Assert.Equal("twice daily", created.Frequency);
        Assert.Equal("Give with food", created.Instructions);
        Assert.Equal("Amoxicillin", created.MedicineName);
        Assert.Equal(100m, created.MedicineUnitPrice);

        var inDb = await context.Prescriptions.FindAsync(created.Id);
        Assert.NotNull(inDb);
        Assert.Equal(MedicineRequestStatus.Pending, inDb!.RequestStatus);
        Assert.Equal(14, inDb.Quantity);
    }

    [Fact]
    public async Task CreateAsync_Veterinarian_CannotPrescribeOnOtherVetsExamination()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var vetUserId = Guid.NewGuid();
        var ownVet = new Veterinarian { Id = Guid.NewGuid(), Name = "Dr. Own", UserId = vetUserId, Active = true };
        var otherVet = new Veterinarian { Id = Guid.NewGuid(), Name = "Dr. Other", Active = true };
        context.Veterinarians.AddRange(ownVet, otherVet);
        var medicineId = Guid.NewGuid();
        context.Medicines.Add(new Medicine { Id = medicineId, Name = "Amoxicillin" });
        var treatmentRecord = await SeedClinicalGraphAsync(context, otherVet); // other vet's exam

        var tenant = new TestTenantContext
        {
            UserId = vetUserId,
            Role = Roles.Veterinarian,
            IsPlatformAdmin = false
        };
        var service = new PrescriptionService(context, tenant);
        var dto = new CreatePrescriptionDto
        {
            TreatmentRecordId = treatmentRecord.Id,
            Items = { new CreatePrescriptionItemDto { MedicineId = medicineId, Dosage = "1 pill daily", DurationDays = 5, Quantity = 5 } }
        };

        // Act + Assert
        await Assert.ThrowsAsync<ForbiddenException>(() => service.CreateAsync(dto));
        Assert.Empty(context.Prescriptions);
    }

    [Fact]
    public async Task CreateAsync_Veterinarian_CanPrescribeOnOwnExamination()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var vetUserId = Guid.NewGuid();
        var ownVet = new Veterinarian { Id = Guid.NewGuid(), Name = "Dr. Own", UserId = vetUserId, Active = true };
        context.Veterinarians.Add(ownVet);
        var medicineId = Guid.NewGuid();
        context.Medicines.Add(new Medicine { Id = medicineId, Name = "Amoxicillin" });
        var treatmentRecord = await SeedClinicalGraphAsync(context, ownVet); // own exam

        var tenant = new TestTenantContext
        {
            UserId = vetUserId,
            Role = Roles.Veterinarian,
            IsPlatformAdmin = false
        };
        var service = new PrescriptionService(context, tenant);
        var dto = new CreatePrescriptionDto
        {
            TreatmentRecordId = treatmentRecord.Id,
            Items = { new CreatePrescriptionItemDto { MedicineId = medicineId, Dosage = "1 pill daily", DurationDays = 5, Quantity = 2 } }
        };

        // Act
        var result = await service.CreateAsync(dto);

        // Assert
        var created = Assert.Single(result);
        Assert.Equal("Pending", created.RequestStatus);
        Assert.Equal(2, created.Quantity);
    }

    private static CreatePrescriptionDto RequestWith(
        Guid treatmentRecordId, params CreatePrescriptionItemDto[] items) =>
        new() { TreatmentRecordId = treatmentRecordId, Items = items.ToList() };

    private static CreatePrescriptionItemDto Item(Guid medicineId, int quantity = 1) =>
        new() { MedicineId = medicineId, Dosage = "1 pill daily", DurationDays = 5, Quantity = quantity };

    [Fact]
    public async Task CreateAsync_MultipleMedicines_CreatesOneRowPerItem()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var vet = new Veterinarian { Id = Guid.NewGuid(), Name = "Dr. Vet", Active = true };
        context.Veterinarians.Add(vet);
        var medA = Guid.NewGuid();
        var medB = Guid.NewGuid();
        context.Medicines.AddRange(
            new Medicine { Id = medA, Name = "Amoxicillin", UnitPrice = 100m },
            new Medicine { Id = medB, Name = "Meloxicam", UnitPrice = 90m });
        var treatmentRecord = await SeedClinicalGraphAsync(context, vet);

        var service = new PrescriptionService(context, TestTenantContext.Unscoped);
        var dto = RequestWith(treatmentRecord.Id, Item(medA, 10), Item(medB, 2));

        // Act
        var result = await service.CreateAsync(dto);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal("Pending", r.RequestStatus));
        Assert.All(result, r => Assert.Equal(treatmentRecord.Id, r.TreatmentRecordId));
        Assert.Equal(2, context.Prescriptions.Count());
    }

    [Fact]
    public async Task CreateAsync_TenMedicines_Succeeds()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var vet = new Veterinarian { Id = Guid.NewGuid(), Name = "Dr. Vet", Active = true };
        context.Veterinarians.Add(vet);
        var medIds = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToList();
        context.Medicines.AddRange(medIds.Select((id, i) => new Medicine { Id = id, Name = $"Med{i}", UnitPrice = 10m }));
        var treatmentRecord = await SeedClinicalGraphAsync(context, vet);

        var service = new PrescriptionService(context, TestTenantContext.Unscoped);
        var dto = RequestWith(treatmentRecord.Id, medIds.Select(id => Item(id)).ToArray());

        // Act
        var result = await service.CreateAsync(dto);

        // Assert
        Assert.Equal(10, result.Count);
    }

    [Fact]
    public async Task CreateAsync_DuplicateMedicine_Rejected()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var vet = new Veterinarian { Id = Guid.NewGuid(), Name = "Dr. Vet", Active = true };
        context.Veterinarians.Add(vet);
        var medId = Guid.NewGuid();
        context.Medicines.Add(new Medicine { Id = medId, Name = "Amoxicillin" });
        var treatmentRecord = await SeedClinicalGraphAsync(context, vet);

        var service = new PrescriptionService(context, TestTenantContext.Unscoped);
        var dto = RequestWith(treatmentRecord.Id, Item(medId, 2), Item(medId, 3));

        // Act + Assert — nothing is persisted on a rejected request.
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() => service.CreateAsync(dto));
        Assert.Empty(context.Prescriptions);
    }

    [Fact]
    public async Task CreateAsync_UnknownMedicine_Rejected()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var vet = new Veterinarian { Id = Guid.NewGuid(), Name = "Dr. Vet", Active = true };
        context.Veterinarians.Add(vet);
        var treatmentRecord = await SeedClinicalGraphAsync(context, vet);

        var service = new PrescriptionService(context, TestTenantContext.Unscoped);
        var dto = RequestWith(treatmentRecord.Id, Item(Guid.NewGuid()));

        // Act + Assert
        await Assert.ThrowsAsync<NotFoundException>(() => service.CreateAsync(dto));
        Assert.Empty(context.Prescriptions);
    }
}
