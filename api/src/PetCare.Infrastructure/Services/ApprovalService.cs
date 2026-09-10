using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PetCare.Application.DTOs.Approvals;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;
using PetCare.Infrastructure.Persistence;

namespace PetCare.Infrastructure.Services;

public sealed class ApprovalService : IApprovalService
{
    private readonly PetCareDbContext _dbContext;

    public ApprovalService(PetCareDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AIProposalSummaryDto>> GetProposalsAsync(
        Guid organizationId,
        string? status = null,
        CancellationToken ct = default)
    {
        var query = _dbContext.AIProposals
            .AsNoTracking()
            .Where(p => p.OrganizationId == organizationId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(p => p.Status.ToLower() == status.ToLower());
        }

        var proposals = await query
            .OrderByDescending(p => p.SubmittedAt)
            .ToListAsync(ct);

        return proposals.Select(p => new AIProposalSummaryDto(
            Id: p.Id,
            OrganizationId: p.OrganizationId,
            ConsultationRequestId: p.ConsultationRequestId,
            AppointmentId: p.AppointmentId,
            QuotationId: p.QuotationId,
            Status: p.Status,
            Urgency: p.Urgency,
            PetName: p.PetName,
            OwnerName: p.OwnerName,
            SymptomsSummary: p.SymptomsSummary,
            ProposedVeterinarianName: p.ProposedVeterinarianName,
            ProposedDate: p.ProposedDate,
            ProposedTime: p.ProposedTime,
            QuotationTotal: p.QuotationTotal,
            BudgetLimit: p.BudgetLimit,
            SubmittedAt: p.SubmittedAt
        )).ToList();
    }

    /// <inheritdoc />
    public async Task<AIProposalDetailsDto?> GetProposalByIdAsync(
        Guid id,
        Guid organizationId,
        CancellationToken ct = default)
    {
        var p = await _dbContext.AIProposals
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == organizationId, ct);

        if (p is null) return null;

        var validationChecks = ParseValidationChecks(p.ValidationChecksJson);
        var executionSteps = ParseExecutionSteps(p.ExecutionStepsJson);

        return new AIProposalDetailsDto(
            Id: p.Id,
            OrganizationId: p.OrganizationId,
            ConsultationRequestId: p.ConsultationRequestId,
            AppointmentId: p.AppointmentId,
            QuotationId: p.QuotationId,
            Status: p.Status,
            Urgency: p.Urgency,
            PetName: p.PetName,
            OwnerName: p.OwnerName,
            SymptomsSummary: p.SymptomsSummary,
            PreliminaryRecommendation: p.PreliminaryRecommendation,
            ProposedTreatment: p.ProposedTreatment,
            MedicineAvailabilityStatus: p.MedicineAvailabilityStatus,
            ProposedVeterinarianName: p.ProposedVeterinarianName,
            ProposedDate: p.ProposedDate,
            ProposedTime: p.ProposedTime,
            QuotationTotal: p.QuotationTotal,
            BudgetLimit: p.BudgetLimit,
            ValidationChecks: validationChecks,
            ExecutionSteps: executionSteps,
            SubmittedAt: p.SubmittedAt,
            ReviewedAt: p.ReviewedAt,
            ReviewedBy: p.ReviewedBy,
            DecisionNote: p.DecisionNote
        );
    }

    /// <inheritdoc />
    public async Task<AIProposalDetailsDto> ApproveProposalAsync(
        Guid id,
        Guid organizationId,
        string reviewerId,
        string reviewerEmail,
        ApproveProposalDto request,
        CancellationToken ct = default)
    {
        var proposal = await _dbContext.AIProposals
            .FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == organizationId, ct);

        if (proposal is null)
            throw new KeyNotFoundException("AI Proposal not found in your organization.");

        if (proposal.Status != "Pending")
            throw new InvalidOperationException($"Proposal cannot be approved because current status is '{proposal.Status}'.");

        // Deterministic validation check
        if (proposal.QuotationTotal > proposal.BudgetLimit)
            throw new InvalidOperationException($"Cannot approve proposal: Quotation total ({proposal.QuotationTotal}) exceeds budget limit ({proposal.BudgetLimit}).");

        var executionStrategy = _dbContext.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                var now = DateTime.UtcNow;

                // 1. Update AIProposal
                proposal.Status = "Approved";
                proposal.ReviewedAt = now;
                proposal.ReviewedBy = reviewerEmail;
                proposal.DecisionNote = request.Comment;

                // 2. Update or create Quotation & Approval
                Guid.TryParse(reviewerId, out var parsedReviewerId);
                Quotation? quotation = null;

                if (proposal.QuotationId.HasValue)
                {
                    quotation = await _dbContext.Quotations
                        .Include(q => q.Approval)
                        .FirstOrDefaultAsync(q => q.Id == proposal.QuotationId.Value, ct);
                }

                if (quotation == null)
                {
                    quotation = new Quotation
                    {
                        Id = Guid.NewGuid(),
                        AppointmentId = proposal.AppointmentId ?? Guid.NewGuid(),
                        Budget = proposal.BudgetLimit,
                        Subtotal = proposal.QuotationTotal,
                        Total = proposal.QuotationTotal,
                        Status = QuotationStatus.Approved,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    };
                    _dbContext.Quotations.Add(quotation);
                    proposal.QuotationId = quotation.Id;
                }
                else
                {
                    quotation.Status = QuotationStatus.Approved;
                    quotation.UpdatedAt = DateTimeOffset.UtcNow;
                }

                if (quotation.Approval == null)
                {
                    quotation.Approval = new Approval
                    {
                        Id = Guid.NewGuid(),
                        QuotationId = quotation.Id,
                        Status = ApprovalStatus.Approved,
                        ReviewedBy = parsedReviewerId != Guid.Empty ? parsedReviewerId : null,
                        ReviewedAt = DateTimeOffset.UtcNow,
                        Comment = request.Comment
                    };
                    _dbContext.Approvals.Add(quotation.Approval);
                }
                else
                {
                    quotation.Approval.Status = ApprovalStatus.Approved;
                    quotation.Approval.ReviewedBy = parsedReviewerId != Guid.Empty ? parsedReviewerId : null;
                    quotation.Approval.ReviewedAt = DateTimeOffset.UtcNow;
                    quotation.Approval.Comment = request.Comment;
                }

                _dbContext.ApprovalHistories.Add(new ApprovalHistory
                {
                    Id = Guid.NewGuid(),
                    ApprovalId = quotation.Approval.Id,
                    PreviousStatus = "Pending",
                    NewStatus = ApprovalStatus.Approved.ToString(),
                    ChangedBy = parsedReviewerId,
                    Reason = request.Comment ?? "Approved by Clinic Manager",
                    ChangedAt = DateTimeOffset.UtcNow
                });

                // 3. Finalize Appointment & Slot if linked
                if (proposal.AppointmentId.HasValue)
                {
                    var appointment = await _dbContext.Appointments
                        .Include(a => a.AppointmentSlot)
                        .FirstOrDefaultAsync(a => a.Id == proposal.AppointmentId.Value, ct);

                    if (appointment != null)
                    {
                        appointment.Status = AppointmentStatus.Confirmed;
                        appointment.UpdatedAt = DateTimeOffset.UtcNow;

                        if (appointment.AppointmentSlot != null)
                        {
                            appointment.AppointmentSlot.Status = AppointmentSlotStatus.Confirmed;
                            appointment.AppointmentSlot.UpdatedAt = DateTimeOffset.UtcNow;
                        }
                    }
                }

                // 4. Update ConsultationRequest if linked
                if (!string.IsNullOrWhiteSpace(proposal.ConsultationRequestId))
                {
                    var cr = await _dbContext.ConsultationRequests
                        .FirstOrDefaultAsync(r => r.Id == proposal.ConsultationRequestId, ct);
                    if (cr != null)
                    {
                        cr.Status = "Approved";
                        cr.UpdatedAt = now;
                    }
                }

                // 5. Append Audit Log
                _dbContext.AuditLogs.Add(new AuditLog
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    UserId = reviewerId,
                    UserEmail = reviewerEmail,
                    Action = "ProposalApproved",
                    EntityType = "AIProposal",
                    EntityId = proposal.Id.ToString(),
                    Details = $"Approved AI proposal for pet '{proposal.PetName}'. Decision note: {request.Comment ?? "None"}.",
                    Timestamp = now
                });

                await _dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });

        return (await GetProposalByIdAsync(id, organizationId, ct))!;
    }

    /// <inheritdoc />
    public async Task<AIProposalDetailsDto> RejectProposalAsync(
        Guid id,
        Guid organizationId,
        string reviewerId,
        string reviewerEmail,
        RejectProposalDto request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidOperationException("A rejection reason is required.");

        var proposal = await _dbContext.AIProposals
            .FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == organizationId, ct);

        if (proposal is null)
            throw new KeyNotFoundException("AI Proposal not found in your organization.");

        if (proposal.Status != "Pending")
            throw new InvalidOperationException($"Proposal cannot be rejected because current status is '{proposal.Status}'.");

        var executionStrategy = _dbContext.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                var now = DateTime.UtcNow;

                proposal.Status = "Rejected";
                proposal.ReviewedAt = now;
                proposal.ReviewedBy = reviewerEmail;
                proposal.DecisionNote = request.Reason.Trim();

                if (proposal.QuotationId.HasValue)
                {
                    var quotation = await _dbContext.Quotations
                        .Include(q => q.Approval)
                        .FirstOrDefaultAsync(q => q.Id == proposal.QuotationId.Value, ct);

                    if (quotation != null)
                    {
                        quotation.Status = QuotationStatus.Rejected;
                        quotation.UpdatedAt = DateTimeOffset.UtcNow;

                        if (quotation.Approval != null)
                        {
                            var previousStatus = quotation.Approval.Status.ToString();
                            quotation.Approval.Status = ApprovalStatus.Rejected;
                            Guid.TryParse(reviewerId, out var parsedReviewerId);
                            quotation.Approval.ReviewedBy = parsedReviewerId != Guid.Empty ? parsedReviewerId : null;
                            quotation.Approval.ReviewedAt = DateTimeOffset.UtcNow;
                            quotation.Approval.Comment = request.Reason.Trim();

                            _dbContext.ApprovalHistories.Add(new ApprovalHistory
                            {
                                Id = Guid.NewGuid(),
                                ApprovalId = quotation.Approval.Id,
                                PreviousStatus = previousStatus,
                                NewStatus = ApprovalStatus.Rejected.ToString(),
                                ChangedBy = parsedReviewerId,
                                Reason = request.Reason.Trim(),
                                ChangedAt = DateTimeOffset.UtcNow
                            });
                        }
                    }
                }

                // Append Audit Log
                _dbContext.AuditLogs.Add(new AuditLog
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    UserId = reviewerId,
                    UserEmail = reviewerEmail,
                    Action = "ProposalRejected",
                    EntityType = "AIProposal",
                    EntityId = proposal.Id.ToString(),
                    Details = $"Rejected AI proposal for pet '{proposal.PetName}'. Reason: {request.Reason.Trim()}.",
                    Timestamp = now
                });

                await _dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });

        return (await GetProposalByIdAsync(id, organizationId, ct))!;
    }

    /// <inheritdoc />
    public async Task<AIProposalDetailsDto> RequestRevisionAsync(
        Guid id,
        Guid organizationId,
        string reviewerId,
        string reviewerEmail,
        RequestRevisionDto request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new InvalidOperationException("A revision reason is required.");

        var proposal = await _dbContext.AIProposals
            .FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == organizationId, ct);

        if (proposal is null)
            throw new KeyNotFoundException("AI Proposal not found in your organization.");

        if (proposal.Status != "Pending")
            throw new InvalidOperationException($"Cannot request revision because current status is '{proposal.Status}'.");

        var executionStrategy = _dbContext.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                var now = DateTime.UtcNow;

                proposal.Status = "RevisionRequested";
                proposal.ReviewedAt = now;
                proposal.ReviewedBy = reviewerEmail;
                proposal.DecisionNote = request.Reason.Trim();

                if (proposal.QuotationId.HasValue)
                {
                    var quotation = await _dbContext.Quotations
                        .Include(q => q.Approval)
                        .FirstOrDefaultAsync(q => q.Id == proposal.QuotationId.Value, ct);

                    if (quotation != null)
                    {
                        quotation.Status = QuotationStatus.RevisionRequested;
                        quotation.UpdatedAt = DateTimeOffset.UtcNow;

                        if (quotation.Approval != null)
                        {
                            var previousStatus = quotation.Approval.Status.ToString();
                            quotation.Approval.Status = ApprovalStatus.RevisionRequested;
                            Guid.TryParse(reviewerId, out var parsedReviewerId);
                            quotation.Approval.ReviewedBy = parsedReviewerId != Guid.Empty ? parsedReviewerId : null;
                            quotation.Approval.ReviewedAt = DateTimeOffset.UtcNow;
                            quotation.Approval.Comment = request.Reason.Trim();

                            _dbContext.ApprovalHistories.Add(new ApprovalHistory
                            {
                                Id = Guid.NewGuid(),
                                ApprovalId = quotation.Approval.Id,
                                PreviousStatus = previousStatus,
                                NewStatus = ApprovalStatus.RevisionRequested.ToString(),
                                ChangedBy = parsedReviewerId,
                                Reason = request.Reason.Trim(),
                                ChangedAt = DateTimeOffset.UtcNow
                            });
                        }
                    }
                }

                // Append Audit Log
                _dbContext.AuditLogs.Add(new AuditLog
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    UserId = reviewerId,
                    UserEmail = reviewerEmail,
                    Action = "ProposalRevisionRequested",
                    EntityType = "AIProposal",
                    EntityId = proposal.Id.ToString(),
                    Details = $"Requested revision for AI proposal '{proposal.PetName}'. Reason: {request.Reason.Trim()}.",
                    Timestamp = now
                });

                await _dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });

        return (await GetProposalByIdAsync(id, organizationId, ct))!;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ApprovalHistoryDto>> GetApprovalHistoryAsync(
        Guid id,
        Guid organizationId,
        CancellationToken ct = default)
    {
        var proposal = await _dbContext.AIProposals
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.OrganizationId == organizationId, ct);

        if (proposal is null || !proposal.QuotationId.HasValue)
            return Array.Empty<ApprovalHistoryDto>();

        var approval = await _dbContext.Approvals
            .Include(a => a.Histories)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.QuotationId == proposal.QuotationId.Value, ct);

        if (approval is null)
            return Array.Empty<ApprovalHistoryDto>();

        return approval.Histories
            .OrderByDescending(h => h.ChangedAt)
            .Select(h => new ApprovalHistoryDto(
                Id: h.Id,
                ApprovalId: h.ApprovalId,
                PreviousStatus: h.PreviousStatus,
                NewStatus: h.NewStatus,
                ChangedBy: h.ChangedBy,
                Reason: h.Reason,
                ChangedAt: h.ChangedAt
            )).ToList();
    }

    private static IReadOnlyList<ValidationCheckItemDto> ParseValidationChecks(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<ValidationCheckItemDto>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ValidationCheckItemDto>();
        }
        catch
        {
            return new List<ValidationCheckItemDto>();
        }
    }

    private static IReadOnlyList<ExecutionStepItemDto> ParseExecutionSteps(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<ExecutionStepItemDto>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ExecutionStepItemDto>();
        }
        catch
        {
            return new List<ExecutionStepItemDto>();
        }
    }
}
