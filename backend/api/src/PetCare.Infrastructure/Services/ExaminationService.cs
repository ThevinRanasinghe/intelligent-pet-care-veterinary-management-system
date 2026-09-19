using Microsoft.EntityFrameworkCore;
using PetCare.Application.DTOs;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;
using PetCare.Infrastructure;

namespace PetCare.Infrastructure.Services;

public class ExaminationService : IExaminationService
{
    private readonly PetCareDbContext _context;

    public ExaminationService(PetCareDbContext context)
    {
        _context = context;
    }

    public async Task<List<ExaminationResponseDto>> GetAllAsync()
    {
        var examinations = await _context.Examinations
            .AsNoTracking()
            .ToListAsync();

        return examinations.Select(MapToResponseDto).ToList();
    }

    public async Task<ExaminationResponseDto?> GetByIdAsync(Guid id)
    {
        var examination = await _context.Examinations
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id);

        if (examination == null)
            return null;

        return MapToResponseDto(examination);
    }

    public async Task<List<ExaminationResponseDto>> GetByPetIdAsync(string petId)
    {
        var examinations = await _context.Examinations
            .AsNoTracking()
            .Where(e => e.PetId == petId)
            .ToListAsync();

        return examinations.Select(MapToResponseDto).ToList();
    }

    public async Task<ExaminationResponseDto> CreateAsync(CreateExaminationDto dto)
    {
        var examination = new Examination
        {
            Id = Guid.NewGuid(),
            PetId = dto.PetId,
            VeterinarianId = dto.VeterinarianId,
            ConsultationRequestId = dto.ConsultationRequestId,
            Symptoms = dto.Symptoms,
            Notes = dto.Notes,
            ExaminationDate = dto.ExaminationDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Examinations.Add(examination);
        await _context.SaveChangesAsync();

        return MapToResponseDto(examination);
    }

    public async Task<ExaminationResponseDto?> UpdateAsync(Guid id, UpdateExaminationDto dto)
    {
        var examination = await _context.Examinations.FindAsync(id);
        if (examination == null)
            return null;

        examination.Symptoms = dto.Symptoms;
        examination.Notes = dto.Notes;
        examination.ExaminationDate = dto.ExaminationDate;
        examination.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return MapToResponseDto(examination);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var examination = await _context.Examinations.FindAsync(id);
        if (examination == null)
            return false;

        _context.Examinations.Remove(examination);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<TreatmentRecommendationDto> GetRecommendationsAsync(Guid examinationId)
    {
        var exam = await _context.Examinations
            .Include(e => e.Pet)
            .FirstOrDefaultAsync(e => e.Id == examinationId);

        var symptomsLower = (exam?.Symptoms ?? "").ToLowerInvariant();
        var notesLower = (exam?.Notes ?? "").ToLowerInvariant();
        var fullText = $"{symptomsLower} {notesLower}";

        var result = new TreatmentRecommendationDto();

        if (fullText.Contains("vomit") || fullText.Contains("letharg") || fullText.Contains("diarrhea"))
        {
            result.SuspectedCondition = "Acute Gastroenteritis / Dietary Indiscretion";
            result.RecommendedSeverity = "Moderate";
            result.Rationale = "Symptom profile indicates acute gastrointestinal distress with secondary lethargy and dehydration risk.";
            result.RecommendedProcedures = new List<string>
            {
                "Intravenous fluid therapy with balanced electrolyte solution (Lactated Ringer's)",
                "Abdominal palpation and diagnostic ultrasound if discomfort persists",
                "Gradual reintroduction of bland GI gastrointestinal diet"
            };
            result.SuggestedMedicines = new List<RecommendedMedicineDto>
            {
                new RecommendedMedicineDto
                {
                    MedicineId = Guid.Parse("33333333-3333-3333-3333-333333333331"),
                    MedicineName = "Metoclopramide 10mg Tablets",
                    SuggestedDosage = "0.5mg/kg every 8 hours before meals",
                    SuggestedDurationDays = 5
                },
                new RecommendedMedicineDto
                {
                    MedicineId = Guid.Parse("33333333-3333-3333-3333-333333333334"),
                    MedicineName = "Oral Rehydration Electrolyte Solution",
                    SuggestedDosage = "50ml/kg daily divided into small portions",
                    SuggestedDurationDays = 3
                }
            };
            result.PrecautionaryNotes = new List<string>
            {
                "Check for foreign body obstruction before anti-motility treatment",
                "Monitor for fever or persistent melena (blood in stool)"
            };
        }
        else if (fullText.Contains("itch") || fullText.Contains("skin") || fullText.Contains("alopecia") || fullText.Contains("scratch") || fullText.Contains("flea"))
        {
            result.SuspectedCondition = "Allergic Dermatitis / Parasitic Hypersensitivity";
            result.RecommendedSeverity = "High";
            result.Rationale = "Pruritic symptoms and localized alopecia suggest external parasite or environmental contact allergy.";
            result.RecommendedProcedures = new List<string>
            {
                "Medicated chlorhexidine soothing rinse",
                "Skin tape cytology and flea comb examination",
                "Flea/tick preventive application"
            };
            result.SuggestedMedicines = new List<RecommendedMedicineDto>
            {
                new RecommendedMedicineDto
                {
                    MedicineId = Guid.Parse("33333333-3333-3333-3333-333333333335"),
                    MedicineName = "Prednisolone 5mg Anti-Inflammatory",
                    SuggestedDosage = "1 tablet daily for 3 days then taper",
                    SuggestedDurationDays = 7
                },
                new RecommendedMedicineDto
                {
                    MedicineId = Guid.Parse("33333333-3333-3333-3333-333333333332"),
                    MedicineName = "Amoxicillin / Clavulanic Acid 250mg",
                    SuggestedDosage = "12.5mg/kg twice daily",
                    SuggestedDurationDays = 10
                }
            };
            result.PrecautionaryNotes = new List<string>
            {
                "Ensure pet has access to fresh water during steroid therapy",
                "Use protective e-collar to prevent self-mutilation"
            };
        }
        else if (fullText.Contains("ear") || fullText.Contains("shake") || fullText.Contains("odor") || fullText.Contains("discharge"))
        {
            result.SuspectedCondition = "Otitis Externa (Bacterial / Malassezia)";
            result.RecommendedSeverity = "Moderate";
            result.Rationale = "Unilateral head shaking and canal discharge characteristic of secondary ear canal infection.";
            result.RecommendedProcedures = new List<string>
            {
                "Deep external ear canal lavage under mild restraint",
                "Ear swab microscopy (bacteria/yeast identification)"
            };
            result.SuggestedMedicines = new List<RecommendedMedicineDto>
            {
                new RecommendedMedicineDto
                {
                    MedicineId = Guid.Parse("33333333-3333-3333-3333-333333333336"),
                    MedicineName = "Cefalexin 300mg Broad-Spectrum",
                    SuggestedDosage = "1 tablet twice daily with food",
                    SuggestedDurationDays = 10
                }
            };
            result.PrecautionaryNotes = new List<string>
            {
                "Verify tympanic membrane integrity prior to deep flush",
                "Keep ear canal dry after bathing"
            };
        }
        else
        {
            result.SuspectedCondition = "General Clinical Review / Minor Malaise";
            result.RecommendedSeverity = "Low";
            result.Rationale = "Symptoms do not match acute critical patterns. Conservative monitoring and supportive care recommended.";
            result.RecommendedProcedures = new List<string>
            {
                "Standard clinical vital check (heart rate, respiration, temperature)",
                "Hydration skin tent assessment and weight check"
            };
            result.SuggestedMedicines = new List<RecommendedMedicineDto>
            {
                new RecommendedMedicineDto
                {
                    MedicineId = Guid.Parse("33333333-3333-3333-3333-333333333334"),
                    MedicineName = "Oral Rehydration Electrolyte Solution",
                    SuggestedDosage = "Daily oral fluid supplementation as needed",
                    SuggestedDurationDays = 3
                }
            };
            result.PrecautionaryNotes = new List<string>
            {
                "Return for re-examination if symptoms worsen within 48 hours"
            };
        }

        return result;
    }

    private static ExaminationResponseDto MapToResponseDto(Examination examination)
    {
        return new ExaminationResponseDto
        {
            Id = examination.Id,
            PetId = examination.PetId,
            VeterinarianId = examination.VeterinarianId,
            ConsultationRequestId = examination.ConsultationRequestId,
            Symptoms = examination.Symptoms,
            Notes = examination.Notes,
            ExaminationDate = examination.ExaminationDate,
            CreatedAt = examination.CreatedAt
        };
    }
}
