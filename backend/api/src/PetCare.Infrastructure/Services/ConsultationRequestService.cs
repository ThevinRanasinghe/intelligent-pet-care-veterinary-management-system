using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PetCare.Application.DTOs.Consultations;
using PetCare.Application.Exceptions;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;
using System.Text.Json;

namespace PetCare.Infrastructure.Services;

public class ConsultationRequestService : IConsultationRequestService
{
    private readonly IPetCareDbContext _context;
    private readonly ITenantContext _tenant;
    private readonly IAgenticClient? _agenticClient;
    private readonly ILogger<ConsultationRequestService>? _logger;

    public ConsultationRequestService(
        IPetCareDbContext context,
        ITenantContext tenant,
        IAgenticClient? agenticClient = null,
        ILogger<ConsultationRequestService>? logger = null)
    {
        _context = context;
        _tenant = tenant;
        _agenticClient = agenticClient;
        _logger = logger;
    }

    // =========================================================
    // CREATE CONSULTATION REQUEST
    // =========================================================
    public async Task<ConsultationRequestDto> CreateAsync(
        CreateConsultationRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.OwnerId))
            throw new ArgumentException("Owner ID is required.");

        if (string.IsNullOrWhiteSpace(dto.PetId))
            throw new ArgumentException("Pet ID is required.");

        if (string.IsNullOrWhiteSpace(dto.Symptoms))
            throw new ArgumentException("Symptoms are required.");

        if (dto.Budget.HasValue && dto.Budget.Value < 0)
            throw new ArgumentException("Budget cannot be negative.");

        if (dto.OrganizationId == Guid.Empty)
            throw new ArgumentException("An organization (clinic) must be selected.");

        if (!dto.PreferredDate.HasValue)
            throw new ArgumentException("Preferred date is required.");

        if (!dto.PreferredTime.HasValue)
            throw new ArgumentException("A time slot is required.");

        var slotDate = DateOnly.FromDateTime(dto.PreferredDate.Value);
        var slotStart = TimeOnly.FromTimeSpan(dto.PreferredTime.Value);

        if (!BookingRules.IsHourAligned(slotStart) ||
            !BookingRules.IsWithinOperatingHours(slotStart))
        {
            throw new ArgumentException(
                "Appointments start on the hour between 09:00 and 17:00 (one-hour slots, closing at 18:00).");
        }

        if (!BookingRules.IsInFuture(slotDate, slotStart))
        {
            throw new ArgumentException(
                "The selected date/time slot cannot be in the past.");
        }

        var organization = await RequireActiveOrganizationAsync(
            dto.OrganizationId);

        // The hour is bookable only while at least one active veterinarian
        // of the clinic has no overlapping appointment.
        await EnsureSlotAvailableAsync(
            organization.Id, slotDate, slotStart);

        // Check whether owner exists
        var ownerExists = await _context.PetOwners
            .AnyAsync(x => x.Id == dto.OwnerId.Trim());

        if (!ownerExists)
            throw new ArgumentException("Pet owner was not found.");

        // Check whether pet exists and belongs to this owner
        var pet = await _context.Pets
            .FirstOrDefaultAsync(x =>
                x.Id == dto.PetId.Trim() &&
                x.OwnerId == dto.OwnerId.Trim());

        if (pet == null)
            throw new ArgumentException(
                "Pet was not found or does not belong to this owner.");

        // Archived pets keep their history but cannot start new
        // operational workflows.
        if (pet.IsArchived)
        {
            throw new ArgumentException(
                "This pet is archived and cannot be used for a new consultation request. Restore the pet first.");
        }

        var consultation = new ConsultationRequest
        {
            Id = $"CON-{Guid.NewGuid():N}"
                .Substring(0, 12)
                .ToUpper(),

            PetId = dto.PetId.Trim(),

            OwnerId = dto.OwnerId.Trim(),

            OrganizationId = organization.Id,

            SymptomsDescription = dto.Symptoms.Trim(),

            PhotoUrl = string.IsNullOrWhiteSpace(dto.SymptomPhotoUrl)
                ? null
                : dto.SymptomPhotoUrl.Trim(),

            // The preferred date column carries the booked slot start:
            // booking date + hour-aligned slot start, stored as UTC-kind.
            // PostgreSQL timestamp with time zone requires UTC DateTime.
            PreferredDate = DateTime.SpecifyKind(
                dto.PreferredDate.Value.Date + dto.PreferredTime.Value,
                DateTimeKind.Utc),

            BudgetLimit = dto.Budget ?? 0,

            PreferredClinicLocationLat =
                dto.Latitude.HasValue
                    ? (double?)dto.Latitude.Value
                    : null,

            PreferredClinicLocationLong =
                dto.Longitude.HasValue
                    ? (double?)dto.Longitude.Value
                    : null,

            PreferredBranch = null,

            Status = "Draft",

            StatusNotes = string.IsNullOrWhiteSpace(dto.AdditionalNotes)
                ? null
                : dto.AdditionalNotes.Trim(),

            CreatedAt = DateTime.UtcNow,

            UpdatedAt = DateTime.UtcNow
        };

        _context.ConsultationRequests.Add(consultation);

        await _context.SaveChangesAsync();

        return await GetByIdAsync(consultation.Id)
            ?? throw new InvalidOperationException(
                "Consultation request could not be retrieved after creation.");
    }


    // =========================================================
    // GET ALL CONSULTATION REQUESTS
    // =========================================================
    public async Task<List<ConsultationRequestDto>> GetAllAsync()
    {
        var query = _context.ConsultationRequests
            .AsNoTracking()
            .Include(x => x.Pet)
            .Include(x => x.Organization)
            .Include(x => x.RequestedByVeterinarian)
            .AsQueryable();

        query = await ApplyOrganizationScopeAsync(query);

        // Materialize before mapping — MapToDto is not translatable to SQL.
        var consultations = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return consultations.Select(MapToDto).ToList();
    }


    // =========================================================
    // GET CONSULTATION REQUESTS BY OWNER
    // =========================================================
    public async Task<List<ConsultationRequestDto>> GetByOwnerIdAsync(
        string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            throw new ArgumentException("Owner ID is required.");

        var query = _context.ConsultationRequests
            .AsNoTracking()
            .Include(x => x.Pet)
            .Include(x => x.Organization)
            .Include(x => x.RequestedByVeterinarian)
            .Where(x => x.OwnerId == ownerId.Trim());

        query = await ApplyOrganizationScopeAsync(query);

        // Materialize before mapping — MapToDto is not translatable to SQL.
        var consultations = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return consultations.Select(MapToDto).ToList();
    }


    // =========================================================
    // GET CONSULTATION REQUEST BY ID
    // =========================================================
    public async Task<ConsultationRequestDto?> GetByIdAsync(
        string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var consultation = await _context.ConsultationRequests
            .AsNoTracking()
            .Include(x => x.Pet)
            .Include(x => x.Organization)
            .Include(x => x.RequestedByVeterinarian)
            .FirstOrDefaultAsync(x => x.Id == id.Trim());

        if (consultation == null)
            return null;

        // Foreign-organization requests are invisible to scoped staff.
        if (!await CanAccessAsync(consultation))
            return null;

        return MapToDto(consultation);
    }


    // =========================================================
    // UPDATE CONSULTATION REQUEST
    // =========================================================
    public async Task<ConsultationRequestDto?> UpdateAsync(
        string id,
        UpdateConsultationRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        if (string.IsNullOrWhiteSpace(dto.Symptoms))
            throw new ArgumentException("Symptoms are required.");

        if (dto.Budget.HasValue && dto.Budget.Value < 0)
            throw new ArgumentException("Budget cannot be negative.");

        var consultation = await _context.ConsultationRequests
            .FirstOrDefaultAsync(x => x.Id == id.Trim());

        if (consultation == null)
            return null;

        if (!await CanAccessAsync(consultation))
            return null;

        // Only Draft or RevisionRequired requests
        // can be edited.
        if (consultation.Status != "Draft" &&
            consultation.Status != "RevisionRequired")
        {
            throw new InvalidOperationException(
                "This consultation request cannot be updated in its current status.");
        }

        consultation.SymptomsDescription =
            dto.Symptoms.Trim();

        consultation.PhotoUrl =
            string.IsNullOrWhiteSpace(dto.SymptomPhotoUrl)
                ? null
                : dto.SymptomPhotoUrl.Trim();

        if (dto.OrganizationId.HasValue)
        {
            var organization =
                await RequireActiveOrganizationAsync(
                    dto.OrganizationId.Value);

            consultation.OrganizationId = organization.Id;
        }

        if (dto.PreferredDate.HasValue && dto.PreferredTime.HasValue)
        {
            var slotDate = DateOnly.FromDateTime(dto.PreferredDate.Value);
            var slotStart = TimeOnly.FromTimeSpan(dto.PreferredTime.Value);

            if (!BookingRules.IsHourAligned(slotStart) ||
                !BookingRules.IsWithinOperatingHours(slotStart))
            {
                throw new ArgumentException(
                    "Appointments start on the hour between 09:00 and 17:00 (one-hour slots, closing at 18:00).");
            }

            if (!BookingRules.IsInFuture(slotDate, slotStart))
            {
                throw new ArgumentException(
                    "The selected date/time slot cannot be in the past.");
            }

            // Store booking date + slot start together (UTC-kind).
            consultation.PreferredDate =
                DateTime.SpecifyKind(
                    dto.PreferredDate.Value.Date +
                    dto.PreferredTime.Value,
                    DateTimeKind.Utc);
        }

        if (dto.Budget.HasValue)
        {
            consultation.BudgetLimit =
                dto.Budget.Value;
        }

        consultation.PreferredClinicLocationLat =
            dto.Latitude.HasValue
                ? (double?)dto.Latitude.Value
                : null;

        consultation.PreferredClinicLocationLong =
            dto.Longitude.HasValue
                ? (double?)dto.Longitude.Value
                : null;

        consultation.StatusNotes =
            string.IsNullOrWhiteSpace(dto.AdditionalNotes)
                ? null
                : dto.AdditionalNotes.Trim();

        consultation.UpdatedAt =
            DateTime.UtcNow;

        // Re-check slot capacity at the (possibly changed) clinic/date/time
        // so a moved request never lands on a fully-booked slot.
        if (consultation.OrganizationId.HasValue)
        {
            await EnsureSlotAvailableAsync(
                consultation.OrganizationId.Value,
                DateOnly.FromDateTime(consultation.PreferredDate),
                TimeOnly.FromDateTime(consultation.PreferredDate),
                ignoreConsultationId: consultation.Id);
        }

        await _context.SaveChangesAsync();

        return await GetByIdAsync(id);
    }


    // =========================================================
    // SUBMIT CONSULTATION REQUEST
    // =========================================================
    public async Task<ConsultationRequestDto?> SubmitAsync(
        string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var consultation = await _context.ConsultationRequests
            .Include(x => x.Pet)
            .FirstOrDefaultAsync(x => x.Id == id.Trim());

        if (consultation == null)
            return null;

        if (!await CanAccessAsync(consultation))
            return null;

        // Only Draft requests can be submitted
        if (consultation.Status != "Draft")
        {
            throw new InvalidOperationException(
                "Only draft consultation requests can be submitted.");
        }

        // Verify pet ownership again
        if (consultation.Pet == null ||
            consultation.Pet.OwnerId != consultation.OwnerId)
        {
            throw new InvalidOperationException(
                "The selected pet does not belong to the request owner.");
        }

        if (string.IsNullOrWhiteSpace(
                consultation.SymptomsDescription))
        {
            throw new ArgumentException(
                "Symptoms are required before submission.");
        }

        if (consultation.BudgetLimit < 0)
        {
            throw new ArgumentException(
                "Budget cannot be negative.");
        }

        // The slot may have filled since the request was created — the
        // backend is the final authority on availability.
        if (consultation.OrganizationId.HasValue)
        {
            await EnsureSlotAvailableAsync(
                consultation.OrganizationId.Value,
                DateOnly.FromDateTime(consultation.PreferredDate),
                TimeOnly.FromDateTime(consultation.PreferredDate),
                ignoreConsultationId: consultation.Id);
        }

        consultation.Status = "Submitted";

        consultation.StatusNotes =
            "Consultation request submitted by pet owner.";

        consultation.UpdatedAt =
            DateTime.UtcNow;

        // Add status history
        var history = new ConsultationStatusHistory
        {
            ConsultationRequestId =
                consultation.Id,

            Status = "Submitted",

            Comments =
                "Consultation request submitted by pet owner.",

            ChangedAt = DateTime.UtcNow
        };

        _context.ConsultationStatusHistories
            .Add(history);

        await _context.SaveChangesAsync();

        return await GetByIdAsync(id);
    }


    // =========================================================
    // CANCEL CONSULTATION REQUEST
    // =========================================================
    public async Task<bool> CancelAsync(
        string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;

        var consultation = await _context.ConsultationRequests
            .FirstOrDefaultAsync(x => x.Id == id.Trim());

        if (consultation == null)
            return false;

        if (!await CanAccessAsync(consultation))
            return false;

        if (consultation.Status == "Cancelled")
            return false;

        // Cannot cancel approved/confirmed requests
        if (consultation.Status == "Approved" ||
            consultation.Status == "AppointmentConfirmed")
        {
            throw new InvalidOperationException(
                "This consultation request cannot be cancelled at this stage.");
        }

        var oldStatus = consultation.Status;

        consultation.Status = "Cancelled";

        consultation.StatusNotes =
            "Cancelled by pet owner.";

        consultation.UpdatedAt =
            DateTime.UtcNow;

        // Add status history
        var history = new ConsultationStatusHistory
        {
            ConsultationRequestId =
                consultation.Id,

            Status = "Cancelled",

            Comments =
                $"Status changed from {oldStatus} to Cancelled.",

            ChangedAt = DateTime.UtcNow
        };

        _context.ConsultationStatusHistories
            .Add(history);

        await _context.SaveChangesAsync();

        return true;
    }


    // =========================================================
    // GET CONSULTATION STATUS HISTORY
    // =========================================================
    public async Task<List<ConsultationStatusHistoryDto>>
        GetStatusHistoryAsync(string consultationId)
    {
        if (string.IsNullOrWhiteSpace(consultationId))
        {
            return new List<ConsultationStatusHistoryDto>();
        }

        var consultation =
            await _context.ConsultationRequests
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == consultationId.Trim());

        if (consultation == null ||
            !await CanAccessAsync(consultation))
        {
            throw new ArgumentException(
                "Consultation request was not found.");
        }

        return await _context.ConsultationStatusHistories
            .AsNoTracking()
            .Where(x =>
                x.ConsultationRequestId == consultationId.Trim())
            .OrderBy(x => x.ChangedAt)
            .Select(x => new ConsultationStatusHistoryDto
            {
                Id = x.Id,

                ConsultationRequestId =
                    x.ConsultationRequestId,

                Status =
                    x.Status,

                Comments =
                    x.Comments,

                ChangedAt =
                    x.ChangedAt
            })
            .ToListAsync();
    }


    // =========================================================
    // VALIDATE PET OWNERSHIP
    // =========================================================
    public async Task<bool> ValidateOwnershipAsync(
        string petId,
        string ownerId)
    {
        if (string.IsNullOrWhiteSpace(petId) ||
            string.IsNullOrWhiteSpace(ownerId))
        {
            return false;
        }

        return await _context.Pets
            .AnyAsync(x =>
                x.Id == petId.Trim() &&
                x.OwnerId == ownerId.Trim());
    }


    // =========================================================
    // BOOKING-RULE HELPERS
    // =========================================================

    /// <summary>
    /// Loads the organization and enforces that it exists and is Active.
    /// Unknown id → 404; inactive/pending org → 400.
    /// </summary>
    private async Task<Organization> RequireActiveOrganizationAsync(
        Guid organizationId)
    {
        var organization = await _context.Organizations
            .FirstOrDefaultAsync(x => x.Id == organizationId);

        if (organization == null)
        {
            throw new NotFoundException(
                "The selected organization was not found.");
        }

        if (organization.Status != OrganizationStatus.Active ||
            !organization.IsActive)
        {
            throw new ArgumentException(
                "The selected organization is not active.");
        }

        return organization;
    }

    /// <summary>
    /// A requested (date, start, start+1h) hour is bookable only while at
    /// least one active veterinarian of the organization has no overlapping
    /// non-cancelled appointment. Throws a 409 conflict otherwise.
    /// </summary>
    private async Task EnsureSlotAvailableAsync(
        Guid organizationId,
        DateOnly date,
        TimeOnly start,
        string? ignoreConsultationId = null)
    {
        var veterinarianIds = await _context.Veterinarians
            .Where(v =>
                v.OrganizationId == organizationId &&
                v.Active)
            .Select(v => v.Id)
            .ToListAsync();

        if (veterinarianIds.Count == 0)
        {
            throw new SchedulingConflictException(
                BookingRules.SlotUnavailableMessage);
        }

        var end = start.AddMinutes(BookingRules.SlotDurationMinutes);

        var busyVeterinarianIds = await _context.Appointments
            .Where(a =>
                veterinarianIds.Contains(a.VeterinarianId) &&
                a.Date == date &&
                a.Status != AppointmentStatus.Cancelled &&
                a.StartTime < end &&
                a.EndTime > start &&
                (ignoreConsultationId == null ||
                 a.ConsultationRequestId != ignoreConsultationId))
            .Select(a => a.VeterinarianId)
            .Distinct()
            .ToListAsync();

        if (busyVeterinarianIds.Count >= veterinarianIds.Count)
        {
            throw new SchedulingConflictException(
                BookingRules.SlotUnavailableMessage);
        }
    }

    // =========================================================
    // ORGANIZATION SCOPING HELPERS
    // =========================================================

    /// <summary>
    /// Restricts a query to the caller's organization when the caller is
    /// organization-scoped staff (ClinicManager / Veterinarian /
    /// InventoryOfficer). PetOwner and platform-Admin callers are unscoped.
    /// </summary>
    private async Task<IQueryable<ConsultationRequest>>
        ApplyOrganizationScopeAsync(
            IQueryable<ConsultationRequest> query)
    {
        if (!_tenant.IsOrganizationScoped)
        {
            return query;
        }

        var organizationId =
            await _tenant.GetOrganizationIdAsync();

        return query.Where(x => x.OrganizationId == organizationId);
    }

    // =========================================================
    // AI CONSULTATION ANALYSIS (ADVISORY)
    // =========================================================
    /// <summary>
    /// Advisory AI triage via the agentic consultation agent. The agent
    /// analyses the request over the existing API; its output is
    /// validated and mapped to the analysis contract. Nothing is
    /// persisted — the manager reviews and continues the manual
    /// workflow. Any agent failure returns a safe placeholder marked
    /// Source="unavailable" so request review is never blocked.
    /// </summary>
    public async Task<ConsultationAnalysisDto> GetAnalysisAsync(
        string id,
        string? bearerToken = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            return UnavailableAnalysis(string.Empty);

        var consultation = await _context.ConsultationRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id.Trim());

        // Foreign-organization and unknown requests get the same
        // unavailable response — the endpoint never leaks existence.
        if (consultation == null ||
            !await CanAccessAsync(consultation) ||
            _agenticClient == null)
        {
            return UnavailableAnalysis(id.Trim());
        }

        var result = await _agenticClient.AnalyzeConsultationAsync(
            consultation.Id, bearerToken);

        if (!result.Success || string.IsNullOrWhiteSpace(result.Content))
            return UnavailableAnalysis(consultation.Id);

        ConsultationAnalysisDto? analysis;
        try
        {
            analysis = JsonSerializer.Deserialize<ConsultationAnalysisDto>(
                result.Content);
        }
        catch (JsonException)
        {
            _logger?.LogWarning(
                "Consultation agent returned malformed JSON for request {ConsultationId}",
                consultation.Id);
            return UnavailableAnalysis(consultation.Id);
        }

        if (analysis == null ||
            string.IsNullOrWhiteSpace(analysis.SuggestedNextStep))
        {
            return UnavailableAnalysis(consultation.Id);
        }

        return new ConsultationAnalysisDto
        {
            Source = "agentic-ai",
            ConsultationRequestId = consultation.Id,
            Priority = NormalizeAnalysisValue(
                analysis.Priority,
                new[] { "Low", "Moderate", "High", "Emergency" },
                "Moderate"),
            ConsultationType = NormalizeAnalysisValue(
                analysis.ConsultationType,
                new[] { "Routine", "Urgent", "Emergency" },
                "Routine"),
            KeyConcerns = (analysis.KeyConcerns ?? new())
                .Where(c => !string.IsNullOrWhiteSpace(c.Concern))
                .Select(c => new ConsultationKeyConcernDto
                {
                    Concern = c.Concern.Trim(),
                    Reason = c.Reason?.Trim() ?? string.Empty
                })
                .ToList(),
            RecommendedChecks = CleanAnalysisList(
                analysis.RecommendedChecks),
            SuggestedNextStep = analysis.SuggestedNextStep.Trim(),
            Disclaimer =
                "Preliminary AI consultation assessment — requires veterinary review and confirmation."
        };
    }

    private static ConsultationAnalysisDto UnavailableAnalysis(
        string consultationId) => new()
    {
        Source = "unavailable",
        ConsultationRequestId = consultationId,
        Disclaimer =
            "AI consultation analysis is currently unavailable — continue the normal review and assignment workflow."
    };

    private static string NormalizeAnalysisValue(
        string? value, string[] allowed, string fallback)
    {
        var match = allowed.FirstOrDefault(a =>
            string.Equals(a, value?.Trim(), StringComparison.OrdinalIgnoreCase));
        return match ?? fallback;
    }

    private static List<string> CleanAnalysisList(
        IEnumerable<string>? values) =>
        (values ?? Enumerable.Empty<string>())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim())
            .ToList();

    // =========================================================
    // AI SCHEDULING PLAN (ADVISORY)
    // =========================================================
    /// <summary>
    /// Advisory scheduling/quotation proposal via the agentic scheduling
    /// agent. The agent analyses the request plus available slots over
    /// the existing API; the slot/conflict checks it reports were already
    /// verified against backend data inside the agent. Nothing is
    /// persisted and nothing is booked — the manager still selects the
    /// veterinarian/date/slot and the normal assign endpoint performs
    /// the authoritative validation. Any agent failure returns a safe
    /// placeholder marked Source="unavailable".
    /// </summary>
    public async Task<SchedulingPlanDto> GetSchedulingPlanAsync(
        string id,
        string? bearerToken = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            return UnavailableSchedulingPlan(string.Empty);

        var consultation = await _context.ConsultationRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id.Trim());

        // Foreign-organization and unknown requests get the same
        // unavailable response — the endpoint never leaks existence.
        if (consultation == null ||
            !await CanAccessAsync(consultation) ||
            _agenticClient == null)
        {
            return UnavailableSchedulingPlan(id.Trim());
        }

        var result = await _agenticClient.PlanSchedulingAsync(
            consultation.Id, bearerToken);

        if (!result.Success || string.IsNullOrWhiteSpace(result.Content))
            return UnavailableSchedulingPlan(consultation.Id);

        SchedulingPlanDto? plan;
        try
        {
            plan = JsonSerializer.Deserialize<SchedulingPlanDto>(
                result.Content);
        }
        catch (JsonException)
        {
            _logger?.LogWarning(
                "Scheduling agent returned malformed JSON for request {ConsultationId}",
                consultation.Id);
            return UnavailableSchedulingPlan(consultation.Id);
        }

        // The agent's planningNotes is required by its schema — a payload
        // without it is not a usable proposal.
        if (plan == null ||
            string.IsNullOrWhiteSpace(plan.PlanningNotes))
        {
            return UnavailableSchedulingPlan(consultation.Id);
        }

        return new SchedulingPlanDto
        {
            Source = "agentic-ai",
            RequestId = consultation.Id,
            RecommendedAppointment = CleanAppointment(
                plan.RecommendedAppointment),
            AlternativeSlots = (plan.AlternativeSlots ?? new())
                .Select(CleanAppointment)
                .Where(a => a != null)
                .Cast<SchedulingAppointmentDto>()
                .ToList(),
            QuotationProposal = CleanQuotation(plan.QuotationProposal),
            ValidationSummary = plan.ValidationSummary ?? new(),
            Confidence = NormalizeAnalysisValue(
                plan.Confidence,
                new[] { "Low", "Moderate", "High" },
                "Low"),
            PlanningNotes = plan.PlanningNotes.Trim(),
            Disclaimer =
                "AI-generated scheduling assistance — final availability and appointment rules are determined by the PetCare scheduling system."
        };
    }

    private static SchedulingPlanDto UnavailableSchedulingPlan(
        string requestId) => new()
    {
        Source = "unavailable",
        RequestId = requestId,
        Disclaimer =
            "AI scheduling analysis is currently unavailable — continue the normal scheduling and assignment workflow."
    };

    /// <summary>
    /// Trims a proposed slot; returns null when the proposal is missing
    /// the fields needed to even identify a slot.
    /// </summary>
    private static SchedulingAppointmentDto? CleanAppointment(
        SchedulingAppointmentDto? appointment)
    {
        if (appointment == null ||
            string.IsNullOrWhiteSpace(appointment.Date) ||
            string.IsNullOrWhiteSpace(appointment.StartTime))
        {
            return null;
        }

        return new SchedulingAppointmentDto
        {
            AppointmentSlotId = appointment.AppointmentSlotId?.Trim() ?? string.Empty,
            VeterinarianId = appointment.VeterinarianId?.Trim() ?? string.Empty,
            Date = appointment.Date.Trim(),
            StartTime = appointment.StartTime.Trim(),
            EndTime = appointment.EndTime?.Trim() ?? string.Empty,
            Branch = appointment.Branch?.Trim() ?? string.Empty,
            Reason = appointment.Reason?.Trim() ?? string.Empty
        };
    }

    private static SchedulingQuotationDto? CleanQuotation(
        SchedulingQuotationDto? quotation)
    {
        if (quotation == null)
            return null;

        var items = (quotation.Items ?? new())
            .Where(i => !string.IsNullOrWhiteSpace(i.Description))
            .Select(i => new SchedulingQuotationItemDto
            {
                Category = i.Category?.Trim() ?? string.Empty,
                Description = i.Description.Trim(),
                Quantity = Math.Max(0, i.Quantity),
                UnitPrice = Math.Max(0m, i.UnitPrice),
                Reason = i.Reason?.Trim() ?? string.Empty
            })
            .ToList();

        return new SchedulingQuotationDto
        {
            Budget = Math.Max(0m, quotation.Budget),
            Items = items,
            EstimatedSubtotal = Math.Max(0m, quotation.EstimatedSubtotal),
            EstimatedTotal = Math.Max(0m, quotation.EstimatedTotal),
            WithinBudget = quotation.WithinBudget
        };
    }

    /// <summary>Foreign-organization requests are invisible to scoped staff.</summary>
    private async Task<bool> CanAccessAsync(
        ConsultationRequest consultation)
    {
        if (!_tenant.IsOrganizationScoped)
        {
            return true;
        }

        var organizationId =
            await _tenant.GetOrganizationIdAsync();

        return consultation.OrganizationId == organizationId;
    }


    // =========================================================
    // MAP ENTITY TO DTO
    // =========================================================
    private static ConsultationRequestDto MapToDto(
        ConsultationRequest consultation)
    {
        return new ConsultationRequestDto
        {
            Id = consultation.Id,

            PetId = consultation.PetId,

            OwnerId = consultation.OwnerId,

            PetName =
                consultation.Pet?.Name
                ?? string.Empty,

            Symptoms =
                consultation.SymptomsDescription,

            SymptomPhotoUrl =
                consultation.PhotoUrl,

            // Current entity does not have Urgency.
            // Keep DTO compatibility.
            Urgency = "Medium",

            PreferredDate =
                consultation.PreferredDate,

            // The booked slot start is stored inside PreferredDate
            // (date + hour-aligned time).
            PreferredTime =
                consultation.PreferredDate.TimeOfDay,

            OrganizationId =
                consultation.OrganizationId,

            OrganizationName =
                consultation.Organization?.Name,

            Budget =
                consultation.BudgetLimit,

            Latitude =
                consultation.PreferredClinicLocationLat.HasValue
                    ? (decimal?)consultation.PreferredClinicLocationLat.Value
                    : null,

            Longitude =
                consultation.PreferredClinicLocationLong.HasValue
                    ? (decimal?)consultation.PreferredClinicLocationLong.Value
                    : null,

            AdditionalNotes =
                consultation.StatusNotes,

            Status =
                consultation.Status,

            RequestType =
                consultation.RequestType,

            RequestedByVeterinarianId =
                consultation.RequestedByVeterinarianId,

            RequestedByVeterinarianName =
                consultation.RequestedByVeterinarian?.Name,

            CreatedAt =
                consultation.CreatedAt,

            UpdatedAt =
                consultation.UpdatedAt
        };
    }
}
