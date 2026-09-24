using Microsoft.EntityFrameworkCore;
using PetCare.Application.DTOs;
using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;
using PetCare.Infrastructure;
using PetCare.Infrastructure.Repositories;

namespace PetCare.Infrastructure.Services;

public class DiagnosisService : IDiagnosisService
{
    private readonly PetCareDbContext _context;
    private readonly ITenantContext _tenant;

    public DiagnosisService(PetCareDbContext context, ITenantContext tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    /// <summary>
    /// Diagnoses inherit organization scope from the examination's
    /// veterinarian (Examination -> Veterinarian -> Organization).
    /// </summary>
    private async Task<IQueryable<Diagnosis>> ScopedAsync(CancellationToken ct = default) =>
        await _context.Diagnoses
            .ScopeToOrganizationAsync(_tenant, d => d.Examination!.Veterinarian!.OrganizationId, ct);

    public async Task<List<DiagnosisResponseDto>> GetAllAsync()
    {
        var diagnoses = await (await ScopedAsync())
            .AsNoTracking()
            .ToListAsync();

        return diagnoses.Select(MapToResponseDto).ToList();
    }

    public async Task<DiagnosisResponseDto?> GetByIdAsync(Guid id)
    {
        var diagnosis = await (await ScopedAsync())
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id);

        if (diagnosis == null) return null;

        return MapToResponseDto(diagnosis);
    }

    public async Task<List<DiagnosisResponseDto>> GetByExaminationIdAsync(Guid examinationId)
    {
        var diagnoses = await (await ScopedAsync())
            .AsNoTracking()
            .Where(d => d.ExaminationId == examinationId)
            .ToListAsync();

        return diagnoses.Select(MapToResponseDto).ToList();
    }

    public async Task<DiagnosisResponseDto> CreateAsync(CreateDiagnosisDto dto)
    {
        // The parent examination must belong to the caller's organization —
        // otherwise an org-scoped caller could attach diagnoses to another
        // organization's examination by guessing its id.
        var examinationInScope = await (await _context.Examinations
                .ScopeToOrganizationAsync(_tenant, e => e.Veterinarian!.OrganizationId))
            .AnyAsync(e => e.Id == dto.ExaminationId);
        if (!examinationInScope)
        {
            throw new NotFoundException($"Examination '{dto.ExaminationId}' was not found.");
        }

        var diagnosis = new Diagnosis
        {
            Id = Guid.NewGuid(),
            ExaminationId = dto.ExaminationId,
            ConditionName = dto.ConditionName,
            Description = dto.Description,
            Severity = Enum.Parse<DiagnosisSeverity>(dto.Severity),
            CreatedAt = DateTime.UtcNow
        };

        _context.Diagnoses.Add(diagnosis);
        await _context.SaveChangesAsync();

        return MapToResponseDto(diagnosis);
    }

    public async Task<DiagnosisResponseDto?> UpdateAsync(Guid id, UpdateDiagnosisDto dto)
    {
        var diagnosis = await (await ScopedAsync())
            .FirstOrDefaultAsync(d => d.Id == id);
        if (diagnosis == null) return null;

        diagnosis.ConditionName = dto.ConditionName;
        diagnosis.Description = dto.Description;
        diagnosis.Severity = Enum.Parse<DiagnosisSeverity>(dto.Severity);

        await _context.SaveChangesAsync();

        return MapToResponseDto(diagnosis);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var diagnosis = await (await ScopedAsync())
            .FirstOrDefaultAsync(d => d.Id == id);
        if (diagnosis == null) return false;

        _context.Diagnoses.Remove(diagnosis);
        await _context.SaveChangesAsync();
        return true;
    }

    private static DiagnosisResponseDto MapToResponseDto(Diagnosis diagnosis)
    {
        return new DiagnosisResponseDto
        {
            Id = diagnosis.Id,
            ExaminationId = diagnosis.ExaminationId,
            ConditionName = diagnosis.ConditionName,
            Description = diagnosis.Description,
            Severity = diagnosis.Severity.ToString(),
            CreatedAt = diagnosis.CreatedAt
        };
    }
}
