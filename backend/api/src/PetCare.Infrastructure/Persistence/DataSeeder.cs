using Microsoft.EntityFrameworkCore;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Persistence;

public static class DataSeeder
{
    public static async Task SeedAsync(AppDbContext context)
    {
        // 1. Seed Pets if they do not exist
        var pets = new List<Pet>
        {
            new Pet { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Max", Species = "Dog", Breed = "Golden Retriever" },
            new Pet { Id = Guid.Parse("11111111-1111-1111-1111-111111111112"), Name = "Milo", Species = "Cat", Breed = "British Shorthair" },
            new Pet { Id = Guid.Parse("11111111-1111-1111-1111-111111111113"), Name = "Bella", Species = "Dog", Breed = "Labrador Retriever" },
            new Pet { Id = Guid.Parse("11111111-1111-1111-1111-111111111114"), Name = "Coco", Species = "Cat", Breed = "Persian" },
            new Pet { Id = Guid.Parse("11111111-1111-1111-1111-111111111115"), Name = "Rocky", Species = "Dog", Breed = "German Shepherd" }
        };

        foreach (var pet in pets)
        {
            if (!await context.Pets.AnyAsync(p => p.Id == pet.Id))
            {
                context.Pets.Add(pet);
            }
        }
        await context.SaveChangesAsync();

        // 2. Seed Medicines if they do not exist
        var medicines = new List<Medicine>
        {
            new Medicine { Id = Guid.Parse("33333333-3333-3333-3333-333333333331"), Name = "Metoclopramide 10mg Tablets" },
            new Medicine { Id = Guid.Parse("33333333-3333-3333-3333-333333333332"), Name = "Amoxicillin / Clavulanic Acid 250mg" },
            new Medicine { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Name = "Meloxicam 1.5mg/ml Oral Suspension" },
            new Medicine { Id = Guid.Parse("33333333-3333-3333-3333-333333333334"), Name = "Oral Rehydration Electrolyte Solution" },
            new Medicine { Id = Guid.Parse("33333333-3333-3333-3333-333333333335"), Name = "Prednisolone 5mg Anti-Inflammatory" },
            new Medicine { Id = Guid.Parse("33333333-3333-3333-3333-333333333336"), Name = "Cefalexin 300mg Broad-Spectrum" }
        };

        foreach (var med in medicines)
        {
            if (!await context.Medicines.AnyAsync(m => m.Id == med.Id))
            {
                context.Medicines.Add(med);
            }
        }
        await context.SaveChangesAsync();

        // 3. Seed Realistic Examinations, Diagnoses & Treatments if none exist
        if (await context.Examinations.CountAsync() <= 1)
        {
            var vetId = Guid.Parse("22222222-2222-2222-2222-222222222222");

            // Exam 2: Milo - Dermatitis
            var exam2Id = Guid.Parse("88888888-8888-8888-8888-888888888882");
            if (!await context.Examinations.AnyAsync(e => e.Id == exam2Id))
            {
                var exam2 = new Examination
                {
                    Id = exam2Id,
                    PetId = Guid.Parse("11111111-1111-1111-1111-111111111112"),
                    VeterinarianId = vetId,
                    Symptoms = "Severe itching, alopecia around neck, redness",
                    Notes = "Flea allergy dermatitis suspected. Skin scraping negative for mites.",
                    ExaminationDate = DateTime.UtcNow.AddDays(-3),
                    CreatedAt = DateTime.UtcNow.AddDays(-3),
                    UpdatedAt = DateTime.UtcNow.AddDays(-3)
                };
                context.Examinations.Add(exam2);

                var diag2 = new Diagnosis
                {
                    Id = Guid.NewGuid(),
                    ExaminationId = exam2Id,
                    ConditionName = "Allergic Dermatitis & Secondary Pruritus",
                    Description = "Flea saliva hypersensitivity leading to severe skin excoriation.",
                    Severity = DiagnosisSeverity.High,
                    CreatedAt = DateTime.UtcNow.AddDays(-3)
                };
                context.Diagnoses.Add(diag2);

                var tr2 = new TreatmentRecord
                {
                    Id = Guid.NewGuid(),
                    DiagnosisId = diag2.Id,
                    ProcedureName = "Topical medicated wash and steroid anti-inflammatory protocol",
                    Notes = "Applied topical soothing rinse. Prescribed short course oral steroid.",
                    Status = TreatmentStatus.InProgress,
                    CreatedAt = DateTime.UtcNow.AddDays(-3),
                    UpdatedAt = DateTime.UtcNow.AddDays(-2)
                };
                context.TreatmentRecords.Add(tr2);

                var rx2 = new Prescription
                {
                    Id = Guid.NewGuid(),
                    TreatmentRecordId = tr2.Id,
                    MedicineId = Guid.Parse("33333333-3333-3333-3333-333333333335"),
                    Dosage = "5mg once daily with food",
                    DurationDays = 7,
                    CreatedAt = DateTime.UtcNow.AddDays(-3)
                };
                context.Prescriptions.Add(rx2);
            }

            // Exam 3: Bella - Post-Surgery Checkup
            var exam3Id = Guid.Parse("88888888-8888-8888-8888-888888888883");
            if (!await context.Examinations.AnyAsync(e => e.Id == exam3Id))
            {
                var exam3 = new Examination
                {
                    Id = exam3Id,
                    PetId = Guid.Parse("11111111-1111-1111-1111-111111111113"),
                    VeterinarianId = vetId,
                    Symptoms = "Routine post-operative incision inspection",
                    Notes = "Incision is healing well with no signs of swelling or discharge.",
                    ExaminationDate = DateTime.UtcNow.AddDays(-1),
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    UpdatedAt = DateTime.UtcNow.AddDays(-1)
                };
                context.Examinations.Add(exam3);

                var diag3 = new Diagnosis
                {
                    Id = Guid.NewGuid(),
                    ExaminationId = exam3Id,
                    ConditionName = "Post-Surgical Healing Normal",
                    Description = "Normal recovery course following minor soft tissue procedure.",
                    Severity = DiagnosisSeverity.Low,
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                };
                context.Diagnoses.Add(diag3);

                var tr3 = new TreatmentRecord
                {
                    Id = Guid.NewGuid(),
                    DiagnosisId = diag3.Id,
                    ProcedureName = "Suture line cleaning and protective bandage replacement",
                    Notes = "Cleaned with chlorhexidine solution. Suture removal scheduled next week.",
                    Status = TreatmentStatus.Completed,
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    UpdatedAt = DateTime.UtcNow
                };
                context.TreatmentRecords.Add(tr3);
            }

            // Exam 4: Rocky - Ear Infection
            var exam4Id = Guid.Parse("88888888-8888-8888-8888-888888888884");
            if (!await context.Examinations.AnyAsync(e => e.Id == exam4Id))
            {
                var exam4 = new Examination
                {
                    Id = exam4Id,
                    PetId = Guid.Parse("11111111-1111-1111-1111-111111111115"),
                    VeterinarianId = vetId,
                    Symptoms = "Head shaking, brown discharge and foul odor from right ear canal",
                    Notes = "Otoscopic exam shows marked erythema and ceruminous otitis externa.",
                    ExaminationDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                context.Examinations.Add(exam4);

                var diag4 = new Diagnosis
                {
                    Id = Guid.NewGuid(),
                    ExaminationId = exam4Id,
                    ConditionName = "Otitis Externa (Bacterial/Yeast)",
                    Description = "Unilateral external ear canal infection.",
                    Severity = DiagnosisSeverity.Moderate,
                    CreatedAt = DateTime.UtcNow
                };
                context.Diagnoses.Add(diag4);

                var tr4 = new TreatmentRecord
                {
                    Id = Guid.NewGuid(),
                    DiagnosisId = diag4.Id,
                    ProcedureName = "Deep ear flush and topical antibiotic/anti-fungal instillation",
                    Notes = "Flushed canal gently under light sedation. Prescribed oral broad-spectrum antibiotic.",
                    Status = TreatmentStatus.Planned,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                context.TreatmentRecords.Add(tr4);

                var rx4 = new Prescription
                {
                    Id = Guid.NewGuid(),
                    TreatmentRecordId = tr4.Id,
                    MedicineId = Guid.Parse("33333333-3333-3333-3333-333333333336"),
                    Dosage = "300mg twice daily with meals",
                    DurationDays = 10,
                    CreatedAt = DateTime.UtcNow
                };
                context.Prescriptions.Add(rx4);
            }

            await context.SaveChangesAsync();
        }
    }
}
