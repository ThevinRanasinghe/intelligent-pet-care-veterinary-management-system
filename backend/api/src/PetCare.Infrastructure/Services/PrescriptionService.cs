using Microsoft.EntityFrameworkCore;
using PetCare.Application.DTOs;
using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;
using PetCare.Infrastructure;
using PetCare.Infrastructure.Repositories;

namespace PetCare.Infrastructure.Services;

public class PrescriptionService : IPrescriptionService
{
    private readonly PetCareDbContext _context;
    private readonly ITenantContext _tenant;

    public PrescriptionService(PetCareDbContext context, ITenantContext tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    /// <summary>
    /// Prescriptions inherit organization scope through TreatmentRecord ->
    /// Diagnosis -> Examination -> Veterinarian -> Organization.
    /// </summary>
    private async Task<IQueryable<Prescription>> ScopedAsync(CancellationToken ct = default) =>
        await _context.Prescriptions
            .ScopeToOrganizationAsync(_tenant, p => p.TreatmentRecord!.Diagnosis!.Examination!.Veterinarian!.OrganizationId, ct);

    public async Task<List<PrescriptionResponseDto>> GetAllAsync()
    {
        var prescriptions = await (await ScopedAsync())
            .AsNoTracking()
            .ToListAsync();

        return prescriptions.Select(MapToResponseDto).ToList();
    }

    public async Task<PrescriptionResponseDto?> GetByIdAsync(Guid id)
    {
        var prescription = await (await ScopedAsync())
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (prescription == null) return null;

        return MapToResponseDto(prescription);
    }

    public async Task<List<PrescriptionResponseDto>> GetByTreatmentRecordIdAsync(Guid treatmentRecordId)
    {
        var prescriptions = await (await ScopedAsync())
            .AsNoTracking()
            .Where(p => p.TreatmentRecordId == treatmentRecordId)
            .ToListAsync();

        return prescriptions.Select(MapToResponseDto).ToList();
    }

    public async Task<PrescriptionResponseDto> CreateAsync(CreatePrescriptionDto dto)
    {
        // Both parents must belong to the caller's organization: the
        // treatment record (clinical graph) and the medicine (org-owned
        // inventory catalog).
        var treatmentInScope = await (await _context.TreatmentRecords
                .ScopeToOrganizationAsync(_tenant, t => t.Diagnosis!.Examination!.Veterinarian!.OrganizationId))
            .AnyAsync(t => t.Id == dto.TreatmentRecordId);
        if (!treatmentInScope)
        {
            throw new NotFoundException($"Treatment record '{dto.TreatmentRecordId}' was not found.");
        }

        var medicineInScope = await (await _context.Medicines
                .ScopeToOrganizationAsync(_tenant, m => m.OrganizationId))
            .AnyAsync(m => m.Id == dto.MedicineId);
        if (!medicineInScope)
        {
            throw new NotFoundException($"Medicine '{dto.MedicineId}' was not found.");
        }

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
        var prescription = await (await ScopedAsync())
            .FirstOrDefaultAsync(p => p.Id == id);
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
