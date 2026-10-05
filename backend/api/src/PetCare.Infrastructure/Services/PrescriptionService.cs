using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PetCare.Application.DTOs;
using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;
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

    /// <summary>
    /// Scoped prescriptions with the navigation chain needed for the
    /// denormalised response fields (Medicine, Pet/Owner, Veterinarian).
    /// Required-nav includes act as inner joins, so this is only used where
    /// the clinical graph is expected to be intact.
    /// </summary>
    private async Task<IQueryable<Prescription>> DetailedScopedAsync(CancellationToken ct = default) =>
        (await ScopedAsync(ct))
            .Include(p => p.Medicine)
            .Include(p => p.TreatmentRecord).ThenInclude(t => t!.Diagnosis).ThenInclude(d => d!.Examination).ThenInclude(e => e!.Veterinarian)
            .Include(p => p.TreatmentRecord).ThenInclude(t => t!.Diagnosis).ThenInclude(d => d!.Examination).ThenInclude(e => e!.Pet).ThenInclude(pet => pet!.Owner);

    public async Task<List<PrescriptionResponseDto>> GetAllAsync()
    {
        var prescriptions = await (await ScopedAsync())
            .AsNoTracking()
            .ToListAsync();

        return prescriptions.Select(MapToResponseDto).ToList();
    }

    public async Task<PrescriptionResponseDto?> GetByIdAsync(Guid id)
    {
        var prescription = await (await DetailedScopedAsync())
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (prescription == null) return null;

        return MapToResponseDto(prescription);
    }

    public async Task<List<PrescriptionResponseDto>> GetByTreatmentRecordIdAsync(Guid treatmentRecordId)
    {
        var prescriptions = await (await DetailedScopedAsync())
            .AsNoTracking()
            .Where(p => p.TreatmentRecordId == treatmentRecordId)
            .ToListAsync();

        return prescriptions.Select(MapToResponseDto).ToList();
    }

    public async Task<IReadOnlyList<PrescriptionResponseDto>> CreateAsync(CreatePrescriptionDto dto)
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

        // A veterinarian may only prescribe on their own examinations.
        if (_tenant.IsInRole(Roles.Veterinarian))
        {
            var userId = _tenant.UserId;
            var ownVeterinarian = userId is null
                ? null
                : await _context.Veterinarians
                    .FirstOrDefaultAsync(v => v.UserId == userId.Value);
            if (ownVeterinarian is null)
            {
                throw new ForbiddenException(
                    "Your account is not linked to a veterinarian profile.");
            }
            var attendingVeterinarianId = await _context.TreatmentRecords
                .Where(t => t.Id == dto.TreatmentRecordId)
                .Select(t => (Guid?)t.Diagnosis!.Examination!.VeterinarianId)
                .FirstOrDefaultAsync();
            if (attendingVeterinarianId != ownVeterinarian.Id)
            {
                throw new ForbiddenException(
                    "You can only prescribe on your own examinations.");
            }
        }

        // Request-level rules beyond shape validation: distinct medicines
        // only, and every medicine must belong to the caller's organization.
        var items = dto.Items;
        var medicineIds = items.Select(i => i.MedicineId).ToList();
        if (medicineIds.Distinct().Count() != medicineIds.Count)
        {
            throw new ValidationException(
                "The same medicine cannot appear more than once in a medicine request.");
        }

        var inScopeIds = await (await _context.Medicines
                .ScopeToOrganizationAsync(_tenant, m => m.OrganizationId))
            .Where(m => medicineIds.Contains(m.Id))
            .Select(m => m.Id)
            .ToListAsync();
        var unknown = medicineIds.Except(inScopeIds).ToList();
        if (unknown.Count > 0)
        {
            throw new NotFoundException($"Medicine '{unknown[0]}' was not found.");
        }

        var prescriptions = items.Select(item => new Prescription
        {
            Id = Guid.NewGuid(),
            TreatmentRecordId = dto.TreatmentRecordId,
            MedicineId = item.MedicineId,
            Dosage = item.Dosage,
            DurationDays = item.DurationDays,
            Quantity = item.Quantity,
            Frequency = item.Frequency,
            Route = item.Route,
            Instructions = item.Instructions,
            RequestStatus = MedicineRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        _context.Prescriptions.AddRange(prescriptions);
        await _context.SaveChangesAsync();

        // Reload with the denormalised navigation chain for the response.
        var createdIds = prescriptions.Select(p => p.Id).ToList();
        var saved = await (await DetailedScopedAsync())
            .AsNoTracking()
            .Where(p => createdIds.Contains(p.Id))
            .ToListAsync();

        var byId = saved.ToDictionary(p => p.Id);
        return prescriptions.Select(p => MapToResponseDto(byId.GetValueOrDefault(p.Id) ?? p)).ToList();
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

    private static PrescriptionResponseDto MapToResponseDto(Prescription prescription) =>
        PrescriptionMapper.ToDto(prescription);
}
