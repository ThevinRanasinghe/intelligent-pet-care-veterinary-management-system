using Microsoft.EntityFrameworkCore;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;
using PetCare.Domain.Entities;

namespace PetCare.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IAgentWorkflowRepository"/>.
/// Organization scoping matches the consultation domain: scoped staff only
/// see workflows whose OrganizationId equals their own; foreign-org rows
/// resolve to null (surfaced as 404).
/// </summary>
public class AgentWorkflowRepository : IAgentWorkflowRepository
{
    private readonly PetCareDbContext _context;
    private readonly ITenantContext _tenant;

    public AgentWorkflowRepository(PetCareDbContext context, ITenantContext tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    public async Task<AgentWorkflow?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = await _context.AgentWorkflows
            .ScopeToOrganizationAsync(_tenant, x => x.OrganizationId, cancellationToken);
        return await query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<AgentWorkflow?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = await _context.AgentWorkflows
            .ScopeToOrganizationAsync(_tenant, x => x.OrganizationId, cancellationToken);
        return await query
            .Include(x => x.Steps)
            .Include(x => x.Approvals)
            .Include(x => x.Events)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<AgentWorkflow?> GetByConsultationIdAsync(
        string consultationRequestId, CancellationToken cancellationToken = default)
    {
        var query = await _context.AgentWorkflows
            .ScopeToOrganizationAsync(_tenant, x => x.OrganizationId, cancellationToken);
        return await query
            .Include(x => x.Steps)
            .Include(x => x.Approvals)
            .Include(x => x.Events)
            .FirstOrDefaultAsync(
                x => x.ConsultationRequestId == consultationRequestId, cancellationToken);
    }

    public async Task<AgentWorkflow?> GetDetailsNoTrackingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = await _context.AgentWorkflows
            .ScopeToOrganizationAsync(_tenant, x => x.OrganizationId, cancellationToken);
        return await query
            .AsNoTracking()
            .Include(x => x.Steps)
            .Include(x => x.Approvals)
            .Include(x => x.Events)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<Dictionary<string, string>> GetStatusesAsync(
        IEnumerable<string> consultationRequestIds, CancellationToken cancellationToken = default)
    {
        var ids = consultationRequestIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<string, string>();

        var query = await _context.AgentWorkflows
            .ScopeToOrganizationAsync(_tenant, x => x.OrganizationId, cancellationToken);
        return await query
            .AsNoTracking()
            .Where(x => ids.Contains(x.ConsultationRequestId))
            .ToDictionaryAsync(x => x.ConsultationRequestId, x => x.Status, cancellationToken);
    }

    public async Task<string?> ResolveConsultationIdAsync(
        string eventType, string referenceId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(referenceId, out var refId))
            return null;

        if (eventType == AgentWorkflowEventType.ExaminationRecorded)
        {
            var query = await _context.Examinations
                .ScopeToOrganizationAsync(_tenant, e => e.Veterinarian!.OrganizationId, cancellationToken);
            return await query
                .Where(e => e.Id == refId)
                .Select(e => e.ConsultationRequestId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (eventType == AgentWorkflowEventType.PrescriptionCreated)
        {
            var query = await _context.TreatmentRecords
                .ScopeToOrganizationAsync(
                    _tenant, t => t.Diagnosis!.Examination!.Veterinarian!.OrganizationId,
                    cancellationToken);
            return await query
                .Where(t => t.Id == refId)
                .Select(t => t.Diagnosis!.Examination!.ConsultationRequestId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return null;
    }

    public Task AddAsync(AgentWorkflow workflow, CancellationToken cancellationToken = default)
    {
        _context.AgentWorkflows.Add(workflow);
        return Task.CompletedTask;
    }

    public void AddStep(AgentWorkflowStep step) => _context.AgentWorkflowSteps.Add(step);

    public void AddApproval(AgentWorkflowApproval approval) =>
        _context.AgentWorkflowApprovals.Add(approval);

    public void AddEvent(AgentWorkflowEvent workflowEvent) =>
        _context.AgentWorkflowEvents.Add(workflowEvent);
}
