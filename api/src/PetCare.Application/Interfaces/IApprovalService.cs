using PetCare.Application.DTOs.Approvals;

namespace PetCare.Application.Interfaces;

public interface IApprovalService
{
    Task<IReadOnlyList<AIProposalSummaryDto>> GetProposalsAsync(
        Guid organizationId,
        string? status = null,
        CancellationToken ct = default);

    Task<AIProposalDetailsDto?> GetProposalByIdAsync(
        Guid id,
        Guid organizationId,
        CancellationToken ct = default);

    Task<AIProposalDetailsDto> ApproveProposalAsync(
        Guid id,
        Guid organizationId,
        string reviewerId,
        string reviewerEmail,
        ApproveProposalDto request,
        CancellationToken ct = default);

    Task<AIProposalDetailsDto> RejectProposalAsync(
        Guid id,
        Guid organizationId,
        string reviewerId,
        string reviewerEmail,
        RejectProposalDto request,
        CancellationToken ct = default);

    Task<AIProposalDetailsDto> RequestRevisionAsync(
        Guid id,
        Guid organizationId,
        string reviewerId,
        string reviewerEmail,
        RequestRevisionDto request,
        CancellationToken ct = default);

    Task<IReadOnlyList<ApprovalHistoryDto>> GetApprovalHistoryAsync(
        Guid id,
        Guid organizationId,
        CancellationToken ct = default);
}
