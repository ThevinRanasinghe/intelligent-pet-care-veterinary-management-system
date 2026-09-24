using Microsoft.EntityFrameworkCore;
using PetCare.Application.DTOs;
using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;
using PetCare.Infrastructure;
using PetCare.Infrastructure.Repositories;

namespace PetCare.Infrastructure.Services;

public class TreatmentRecordService : ITreatmentRecordService
{
    private readonly PetCareDbContext _context;
    private readonly ITenantContext _tenant;

    public TreatmentRecordService(PetCareDbContext context, ITenantContext tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    /// <summary>
    /// Treatment records inherit organization scope through
    /// Diagnosis -> Examination -> Veterinarian -> Organization.
    /// </summary>
    private async Task<IQueryable<TreatmentRecord>> ScopedAsync(CancellationToken ct = default) =>
        await _context.TreatmentRecords
            .ScopeToOrganizationAsync(_tenant, t => t.Diagnosis!.Examination!.Veterinarian!.OrganizationId, ct);

    public async Task<List<TreatmentRecordResponseDto>> GetAllAsync()
    {
        var records = await (await ScopedAsync())
            .AsNoTracking()
            .ToListAsync();

        return records.Select(MapToResponseDto).ToList();
    }

    public async Task<TreatmentRecordResponseDto?> GetByIdAsync(Guid id)
    {
        var record = await (await ScopedAsync())
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);

        if (record == null) return null;

        return MapToResponseDto(record);
    }

    public async Task<List<TreatmentRecordResponseDto>> GetByDiagnosisIdAsync(Guid diagnosisId)
    {
        var records = await (await ScopedAsync())
            .AsNoTracking()
            .Where(t => t.DiagnosisId == diagnosisId)
            .ToListAsync();

        return records.Select(MapToResponseDto).ToList();
    }

    public async Task<TreatmentRecordResponseDto> CreateAsync(CreateTreatmentRecordDto dto)
    {
        // The parent diagnosis must belong to the caller's organization.
        var diagnosisInScope = await (await _context.Diagnoses
                .ScopeToOrganizationAsync(_tenant, d => d.Examination!.Veterinarian!.OrganizationId))
            .AnyAsync(d => d.Id == dto.DiagnosisId);
        if (!diagnosisInScope)
        {
            throw new NotFoundException($"Diagnosis '{dto.DiagnosisId}' was not found.");
        }

        var record = new TreatmentRecord
        {
            Id = Guid.NewGuid(),
            DiagnosisId = dto.DiagnosisId,
            ProcedureName = dto.ProcedureName,
            Notes = dto.Notes,
            Status = TreatmentStatus.Planned, // Default status
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.TreatmentRecords.Add(record);
        await _context.SaveChangesAsync();

        return MapToResponseDto(record);
    }

    public async Task<TreatmentRecordResponseDto?> UpdateAsync(Guid id, UpdateTreatmentRecordDto dto)
    {
        var record = await (await ScopedAsync())
            .FirstOrDefaultAsync(t => t.Id == id);
        if (record == null) return null;

        record.ProcedureName = dto.ProcedureName;
        record.Notes = dto.Notes;
        record.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return MapToResponseDto(record);
    }

    public async Task<TreatmentRecordResponseDto?> UpdateStatusAsync(Guid id, UpdateTreatmentStatusDto dto)
    {
        var record = await (await ScopedAsync())
            .FirstOrDefaultAsync(t => t.Id == id);
        if (record == null) return null;

        record.Status = Enum.Parse<TreatmentStatus>(dto.Status);
        record.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return MapToResponseDto(record);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var record = await (await ScopedAsync())
            .FirstOrDefaultAsync(t => t.Id == id);
        if (record == null) return false;

        _context.TreatmentRecords.Remove(record);
        await _context.SaveChangesAsync();
        return true;
    }

    private static TreatmentRecordResponseDto MapToResponseDto(TreatmentRecord record)
    {
        return new TreatmentRecordResponseDto
        {
            Id = record.Id,
            DiagnosisId = record.DiagnosisId,
            ProcedureName = record.ProcedureName,
            Notes = record.Notes,
            Status = record.Status.ToString(),
            CreatedAt = record.CreatedAt,
            UpdatedAt = record.UpdatedAt
        };
    }
}
