using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PetCare.Application.DTOs;
using PetCare.Application.DTOs.Agentic;
using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;
using PetCare.Infrastructure;
using PetCare.Infrastructure.Repositories;
using System.Text.Json;

namespace PetCare.Infrastructure.Services;

public class ExaminationService : IExaminationService
{
    private readonly PetCareDbContext _context;
    private readonly ITenantContext _tenant;
    private readonly IAgenticClient? _agenticClient;
    private readonly ILogger<ExaminationService>? _logger;

    public ExaminationService(
        PetCareDbContext context,
        ITenantContext tenant,
        IAgenticClient? agenticClient = null,
        ILogger<ExaminationService>? logger = null)
    {
        _context = context;
        _tenant = tenant;
        _agenticClient = agenticClient;
        _logger = logger;
    }

    /// <summary>
    /// Examinations belong to the attending veterinarian's organization;
    /// org-scoped staff callers only see their own organization's records.
    /// </summary>
    private async Task<IQueryable<Examination>> ScopedAsync(CancellationToken ct = default) =>
        await _context.Examinations
            .ScopeToOrganizationAsync(_tenant, e => e.Veterinarian!.OrganizationId, ct);

    public async Task<List<ExaminationResponseDto>> GetAllAsync()
    {
        var examinations = await (await ScopedAsync())
            .AsNoTracking()
            .ToListAsync();

        return examinations.Select(MapToResponseDto).ToList();
    }

    public async Task<ExaminationResponseDto?> GetByIdAsync(Guid id)
    {
        var examination = await (await ScopedAsync())
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id);

        if (examination == null)
            return null;

        return MapToResponseDto(examination);
    }

    public async Task<List<ExaminationResponseDto>> GetByPetIdAsync(string petId)
    {
        var examinations = await (await ScopedAsync())
            .AsNoTracking()
            .Where(e => e.PetId == petId)
            .ToListAsync();

        return examinations.Select(MapToResponseDto).ToList();
    }

    public async Task<ExaminationResponseDto> CreateAsync(CreateExaminationDto dto)
    {
        // Veterinarian callers always examine under their own profile — the
        // client-supplied VeterinarianId is ignored for them.
        var veterinarianId = dto.VeterinarianId;
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
            veterinarianId = ownVeterinarian.Id;
        }

        // The attending veterinarian determines the examination's
        // organization; reject references to a veterinarian outside the
        // caller's organization.
        var veterinarianInScope = await (await _context.Veterinarians
                .ScopeToOrganizationAsync(_tenant, v => v.OrganizationId))
            .AnyAsync(v => v.Id == veterinarianId);
        if (!veterinarianInScope)
        {
            throw new NotFoundException($"Veterinarian '{veterinarianId}' was not found.");
        }

        // When completing an appointment, the examination inherits its
        // pet/consultation links and the appointment is marked Completed.
        Appointment? appointment = null;
        if (dto.AppointmentId is not null)
        {
            appointment = await (await _context.Appointments
                    .ScopeToOrganizationAsync(_tenant, a => a.Veterinarian.OrganizationId))
                .Include(a => a.AppointmentSlot)
                .FirstOrDefaultAsync(a => a.Id == dto.AppointmentId.Value);
            if (appointment is null)
            {
                throw new NotFoundException($"Appointment '{dto.AppointmentId}' was not found.");
            }

            if (appointment.VeterinarianId != veterinarianId)
            {
                throw new SchedulingConflictException(
                    "The appointment does not belong to the attending veterinarian.");
            }

            if (appointment.Status is AppointmentStatus.Completed or AppointmentStatus.Cancelled)
            {
                throw new SchedulingConflictException(
                    $"Appointment '{appointment.Id}' is '{appointment.Status}' and cannot be examined.");
            }

            var alreadyExamined = await _context.Examinations
                .AnyAsync(e => e.AppointmentId == appointment.Id);
            if (alreadyExamined)
            {
                throw new SchedulingConflictException(
                    $"Appointment '{appointment.Id}' already has an examination.");
            }
        }

        // Standalone examinations (no appointment) target a pet directly —
        // archived pets cannot start new clinical workflows.
        if (appointment is null && !string.IsNullOrWhiteSpace(dto.PetId))
        {
            var petArchived = await _context.Pets
                .Where(p => p.Id == dto.PetId)
                .Select(p => p.IsArchived)
                .FirstOrDefaultAsync();

            if (petArchived)
            {
                throw new SchedulingConflictException(
                    $"Pet '{dto.PetId}' is archived and cannot be examined. Restore the pet first.");
            }
        }

        var examination = new Examination
        {
            Id = Guid.NewGuid(),
            PetId = appointment?.PetId ?? dto.PetId,
            VeterinarianId = veterinarianId,
            ConsultationRequestId = appointment?.ConsultationRequestId ?? dto.ConsultationRequestId,
            AppointmentId = appointment?.Id,
            Symptoms = dto.Symptoms,
            Notes = dto.Notes,
            // Clients send local-time or unspecified-kind timestamps; timestamptz requires UTC.
            // ToUniversalTime treats Unspecified as local and leaves Utc untouched.
            ExaminationDate = dto.ExaminationDate.ToUniversalTime(),
            VeterinarianCharge = dto.VeterinarianCharge,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Examinations.Add(examination);

        if (appointment is not null)
        {
            appointment.Status = AppointmentStatus.Completed;
            if (appointment.AppointmentSlot is not null)
            {
                appointment.AppointmentSlot.Status = AppointmentSlotStatus.Completed;
            }
        }

        await _context.SaveChangesAsync();

        return MapToResponseDto(examination);
    }

    public async Task<ExaminationResponseDto?> UpdateAsync(Guid id, UpdateExaminationDto dto)
    {
        var examination = await (await ScopedAsync())
            .FirstOrDefaultAsync(e => e.Id == id);
        if (examination == null)
            return null;

        examination.Symptoms = dto.Symptoms;
        examination.Notes = dto.Notes;
        examination.ExaminationDate = dto.ExaminationDate.ToUniversalTime();
        examination.VeterinarianCharge = dto.VeterinarianCharge;
        examination.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return MapToResponseDto(examination);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var examination = await (await ScopedAsync())
            .FirstOrDefaultAsync(e => e.Id == id);
        if (examination == null)
            return false;

        _context.Examinations.Remove(examination);
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// Business-specific operation: advisory AI recommendations via the
    /// agentic diagnosis agent. The agent analyses the examination over the
    /// existing API; its output is validated and mapped to the existing
    /// contract. Nothing is persisted — the vet reviews, edits and saves
    /// manually. Any agent failure returns a safe placeholder marked
    /// Source="unavailable" so the clinical workflow is never blocked.
    /// </summary>
    public async Task<TreatmentRecommendationDto> GetRecommendationsAsync(Guid examinationId, string? bearerToken = null)
    {
        var exists = await (await ScopedAsync()).AnyAsync(e => e.Id == examinationId);
        if (!exists || _agenticClient == null)
            return UnavailableRecommendation();

        var result = await _agenticClient.AnalyzeDiagnosisAsync(examinationId.ToString(), bearerToken);
        if (!result.Success || string.IsNullOrWhiteSpace(result.Content))
            return UnavailableRecommendation();

        DiagnosisAgentResponse? assessment;
        try
        {
            assessment = JsonSerializer.Deserialize<DiagnosisAgentResponse>(result.Content);
        }
        catch (JsonException)
        {
            _logger?.LogWarning("Diagnosis agent returned malformed JSON for examination {ExaminationId}", examinationId);
            return UnavailableRecommendation();
        }

        if (assessment == null || string.IsNullOrWhiteSpace(assessment.SuspectedCondition))
            return UnavailableRecommendation();

        var medicines = new List<RecommendedMedicineDto>();
        foreach (var med in assessment.SuggestedMedicines ?? Enumerable.Empty<AgenticSuggestedMedicine>())
        {
            if (string.IsNullOrWhiteSpace(med.MedicineName))
                continue;

            medicines.Add(new RecommendedMedicineDto
            {
                MedicineId = await ResolveMedicineIdAsync(med.MedicineName),
                MedicineName = med.MedicineName.Trim(),
                SuggestedDosage = med.SuggestedDosage?.Trim() ?? string.Empty,
                SuggestedDurationDays = med.SuggestedDurationDays > 0 ? med.SuggestedDurationDays : 7
            });
        }

        return new TreatmentRecommendationDto
        {
            Source = "agentic-ai",
            SuspectedCondition = assessment.SuspectedCondition.Trim(),
            RecommendedSeverity = NormalizeSeverity(assessment.RecommendedSeverity),
            Rationale = assessment.Rationale?.Trim() ?? string.Empty,
            RecommendedProcedures = CleanList(assessment.RecommendedProcedures),
            SuggestedMedicines = medicines,
            PrecautionaryNotes = CleanList(assessment.PrecautionaryNotes)
        };
    }

    /// <summary>
    /// Resolves an AI-suggested medicine name against the caller's
    /// organization-scoped medicine catalogue. Only a clear, unique match
    /// populates the id; anything else stays null — never fabricated.
    /// Candidates are fetched via a translatable containment filter on the
    /// leading token, then compared by exact normalized name variants.
    /// </summary>
    private async Task<Guid?> ResolveMedicineIdAsync(string medicineName)
    {
        var norm = NormalizeName(medicineName);
        var firstToken = norm.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrEmpty(firstToken))
            return null;

        var query = await _context.Medicines
            .ScopeToOrganizationAsync(_tenant, m => m.OrganizationId);

        var candidates = await query
            .AsNoTracking()
            .Where(m => m.Name.ToLower().Contains(firstToken))
            .Take(25)
            .ToListAsync();

        var matches = candidates
            .Where(m => new[]
                {
                    NormalizeName(m.Name),
                    NormalizeName($"{m.Name} {m.Strength}"),
                    NormalizeName($"{m.Name} {m.DosageForm}"),
                    NormalizeName($"{m.Name} {m.Strength} {m.DosageForm}")
                }.Contains(norm))
            .ToList();

        if (matches.Count == 1)
            return matches[0].Id;

        if (matches.Count > 1)
            _logger?.LogInformation("AI medicine suggestion '{MedicineName}' matched {Count} catalogue items; id left unresolved", medicineName, matches.Count);

        return null;
    }

    private static string NormalizeName(string? value) =>
        string.Join(' ', (value ?? string.Empty).Trim().ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static string NormalizeSeverity(string? severity) =>
        severity?.Trim().ToLowerInvariant() switch
        {
            "low" => "Low",
            "high" => "High",
            "critical" => "Critical",
            _ => "Moderate"
        };

    private static List<string> CleanList(IEnumerable<string>? values) =>
        (values ?? Enumerable.Empty<string>())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim())
            .ToList();

    private static TreatmentRecommendationDto UnavailableRecommendation() => new()
    {
        Source = "unavailable",
        SuspectedCondition = "AI recommendation unavailable",
        RecommendedSeverity = "Moderate",
        Rationale = "The AI advisory service is currently unavailable. Proceed with a manual clinical assessment.",
        RecommendedProcedures = new List<string>(),
        SuggestedMedicines = new List<RecommendedMedicineDto>(),
        PrecautionaryNotes = new List<string> { "Rely on veterinarian clinical judgement." }
    };

    private static ExaminationResponseDto MapToResponseDto(Examination examination) =>
        ExaminationMapper.ToDto(examination);
}
