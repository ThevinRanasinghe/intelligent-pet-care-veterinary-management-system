using Microsoft.EntityFrameworkCore;
using PetCare.Application.DTOs;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;
using PetCare.Infrastructure.Persistence;

namespace PetCare.Infrastructure.Services;

public class PrescriptionService : IPrescriptionService
{
    private readonly AppDbContext _context;

    public PrescriptionService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<PrescriptionResponseDto>> GetAllAsync()
    {
        var prescriptions = await _context.Prescriptions
            .AsNoTracking()
            .ToListAsync();

        return prescriptions.Select(MapToResponseDto).ToList();
    }

    public async Task<PrescriptionResponseDto?> GetByIdAsync(Guid id)
    {
        var prescription = await _context.Prescriptions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (prescription == null) return null;

        return MapToResponseDto(prescription);
    }

    public async Task<List<PrescriptionResponseDto>> GetByTreatmentRecordIdAsync(Guid treatmentRecordId)
    {
        var prescriptions = await _context.Prescriptions
            .AsNoTracking()
            .Where(p => p.TreatmentRecordId == treatmentRecordId)
            .ToListAsync();

        return prescriptions.Select(MapToResponseDto).ToList();
    }

    public async Task<PrescriptionResponseDto> CreateAsync(CreatePrescriptionDto dto)
    {
        var prescription = new Prescription
        {
            Id = Guid.NewGuid(),
            TreatmentRecordId = dto.TreatmentRecordId,
            MedicineId = dto.MedicineId,
            Dosage = dto.Dosage,
            DurationDays = dto.DurationDays,
            CreatedAt = DateTime.UtcNow
        };

        _context.Prescriptions.Add(prescription);
        await _context.SaveChangesAsync();

        return MapToResponseDto(prescription);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var prescription = await _context.Prescriptions.FindAsync(id);
        if (prescription == null) return false;

        _context.Prescriptions.Remove(prescription);
        await _context.SaveChangesAsync();
        return true;
    }

    private static PrescriptionResponseDto MapToResponseDto(Prescription prescription)
    {
        return new PrescriptionResponseDto
        {
            Id = prescription.Id,
            TreatmentRecordId = prescription.TreatmentRecordId,
            MedicineId = prescription.MedicineId,
            Dosage = prescription.Dosage,
            DurationDays = prescription.DurationDays,
            CreatedAt = prescription.CreatedAt
        };
    }
}
