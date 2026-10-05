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

public class PrescriptionServiceTests
{
    private PetCareDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<PetCareDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        
        return new PetCareDbContext(options);
    }

    /// <summary>
    /// The detail query includes required navigations
    /// (Prescription → TreatmentRecord → Diagnosis → Examination →
    /// Veterinarian/Pet → Owner) which act as inner joins, so tests that
    /// read prescriptions back must seed the complete clinical graph.
    /// </summary>
    private static Guid SeedClinicalGraph(PetCareDbContext context)
    {
        var owner = new PetOwner { Id = $"OWN-{Guid.NewGuid():N}", FullName = "Amal Perera", Email = "amal@petcare.lk" };
        var pet = new Pet { Id = $"pet-{Guid.NewGuid():N}", Name = "Shadow", Species = "Dog", Breed = "Labrador", OwnerId = owner.Id, Owner = owner };
        var vet = new Veterinarian { Id = Guid.NewGuid(), Name = "Dr. Silva", Specialisation = "General", Branch = "Colombo" };
        var exam = new Examination
        {
            Id = Guid.NewGuid(),
            PetId = pet.Id,
            Pet = pet,
            VeterinarianId = vet.Id,
            Veterinarian = vet,
            Symptoms = "Vomiting",
            VeterinarianCharge = 2500m,
            ExaminationDate = DateTime.UtcNow
        };
        var diagnosis = new Diagnosis { Id = Guid.NewGuid(), ExaminationId = exam.Id, Examination = exam, ConditionName = "Gastritis", Severity = DiagnosisSeverity.Moderate };
        var treatment = new TreatmentRecord { Id = Guid.NewGuid(), DiagnosisId = diagnosis.Id, Diagnosis = diagnosis, ProcedureName = "Surgery" };
        context.AddRange(owner, pet, vet, exam, diagnosis, treatment);
        return treatment.Id;
    }

    [Fact]
    public async Task CreateAsync_ValidPrescription_ReturnsCreatedDto()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new PrescriptionService(context, TestTenantContext.Unscoped);
        var treatmentRecordId = SeedClinicalGraph(context);
        var medicineId = Guid.NewGuid();
        context.Medicines.Add(new Medicine { Id = medicineId, Name = "Amoxicillin" });
        await context.SaveChangesAsync();

        var dto = new CreatePrescriptionDto
        {
            TreatmentRecordId = treatmentRecordId,
            Items = { new CreatePrescriptionItemDto { MedicineId = medicineId, Dosage = "1 pill daily", DurationDays = 7 } }
        };

        // Act
        var result = await service.CreateAsync(dto);

        // Assert
        var created = Assert.Single(result);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("1 pill daily", created.Dosage);
        Assert.Equal(7, created.DurationDays);

        var inDb = await context.Prescriptions.FindAsync(created.Id);
        Assert.NotNull(inDb);
    }

    [Fact]
    public async Task CreateAsync_AdministrationDetails_PersistedAndReturned()
    {
        // The owner-facing prescription must carry the vet's full
        // administration instructions: route, frequency, instructions,
        // medicine strength and dosage form.
        using var context = GetInMemoryDbContext();
        var service = new PrescriptionService(context, TestTenantContext.Unscoped);
        var treatmentRecordId = SeedClinicalGraph(context);
        var medicineId = Guid.NewGuid();
        context.Medicines.Add(new Medicine { Id = medicineId, Name = "Amoxicillin", Strength = "250 mg", DosageForm = "Tablet", UnitPrice = 120m });
        await context.SaveChangesAsync();

        var dto = new CreatePrescriptionDto
        {
            TreatmentRecordId = treatmentRecordId,
            Items =
            {
                new CreatePrescriptionItemDto
                {
                    MedicineId = medicineId,
                    Dosage = "1 tablet",
                    DurationDays = 5,
                    Quantity = 10,
                    Frequency = "twice daily",
                    Route = "Oral",
                    Instructions = "Give after food"
                }
            }
        };

        var result = await service.CreateAsync(dto);

        var created = Assert.Single(result);
        Assert.Equal("Oral", created.Route);
        Assert.Equal("twice daily", created.Frequency);
        Assert.Equal("Give after food", created.Instructions);
        Assert.Equal(10, created.Quantity);
        Assert.Equal("Amoxicillin", created.MedicineName);
        Assert.Equal("250 mg", created.MedicineStrength);
        Assert.Equal("Tablet", created.MedicineDosageForm);
        Assert.Equal(120m, created.MedicineUnitPrice);
        Assert.StartsWith("RX-", created.PrescriptionNumber);

        var reloaded = await service.GetByIdAsync(created.Id);
        Assert.NotNull(reloaded);
        Assert.Equal("Oral", reloaded!.Route);
        Assert.Equal("Give after food", reloaded.Instructions);
        Assert.Equal("250 mg", reloaded.MedicineStrength);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsDto()
    {
        // Arrange
        using var context = GetInMemoryDbContext();
        var service = new PrescriptionService(context, TestTenantContext.Unscoped);
        
        var treatmentRecordId = SeedClinicalGraph(context);
        var medicine = new Medicine { Id = Guid.NewGuid(), Name = "Amoxicillin" };
        context.Medicines.Add(medicine);
        var prescription = new Prescription
        {
            Id = Guid.NewGuid(),
            TreatmentRecordId = treatmentRecordId,
            MedicineId = medicine.Id,
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
        var service = new PrescriptionService(context, TestTenantContext.Unscoped);

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
        var service = new PrescriptionService(context, TestTenantContext.Unscoped);
        
        var treatmentRecordId1 = SeedClinicalGraph(context);
        var treatmentRecordId2 = SeedClinicalGraph(context);
        var medicine = new Medicine { Id = Guid.NewGuid(), Name = "Amoxicillin" };
        context.Medicines.Add(medicine);

        context.Prescriptions.AddRange(
            new Prescription { Id = Guid.NewGuid(), TreatmentRecordId = treatmentRecordId1, MedicineId = medicine.Id, Dosage = "D1", DurationDays = 1 },
            new Prescription { Id = Guid.NewGuid(), TreatmentRecordId = treatmentRecordId1, MedicineId = medicine.Id, Dosage = "D2", DurationDays = 2 },
            new Prescription { Id = Guid.NewGuid(), TreatmentRecordId = treatmentRecordId2, MedicineId = medicine.Id, Dosage = "D3", DurationDays = 3 }
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
        var service = new PrescriptionService(context, TestTenantContext.Unscoped);
        
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
        var service = new PrescriptionService(context, TestTenantContext.Unscoped);

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
        var service = new PrescriptionService(context, TestTenantContext.Unscoped);
        
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
