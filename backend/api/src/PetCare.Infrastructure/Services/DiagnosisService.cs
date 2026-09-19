using Microsoft.EntityFrameworkCore;
using PetCare.Application.DTOs;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;
using PetCare.Infrastructure;

namespace PetCare.Infrastructure.Services;

public class DiagnosisService : IDiagnosisService
{
    private readonly PetCareDbContext _context;

    public DiagnosisService(PetCareDbContext context)
    {
        _context = context;
    }

    public async Task<List<DiagnosisResponseDto>> GetAllAsync()
    {
        var diagnoses = await _context.Diagnoses
            .AsNoTracking()
            .ToListAsync();

        return diagnoses.Select(MapToResponseDto).ToList();
    }

    public async Task<DiagnosisResponseDto?> GetByIdAsync(Guid id)
    {
        var diagnosis = await _context.Diagnoses
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id);

        if (diagnosis == null) return null;

        return MapToResponseDto(diagnosis);
    }

    public async Task<List<DiagnosisResponseDto>> GetByExaminationIdAsync(Guid examinationId)
    {
        var diagnoses = await _context.Diagnoses
            .AsNoTracking()
            .Where(d => d.ExaminationId == examinationId)
            .ToListAsync();

        return diagnoses.Select(MapToResponseDto).ToList();
    }

    public async Task<DiagnosisResponseDto> CreateAsync(CreateDiagnosisDto dto)
    {
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
        var diagnosis = await _context.Diagnoses.FindAsync(id);
        if (diagnosis == null) return null;

        diagnosis.ConditionName = dto.ConditionName;
        diagnosis.Description = dto.Description;
        diagnosis.Severity = Enum.Parse<DiagnosisSeverity>(dto.Severity);

        await _context.SaveChangesAsync();

        return MapToResponseDto(diagnosis);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var diagnosis = await _context.Diagnoses.FindAsync(id);
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
