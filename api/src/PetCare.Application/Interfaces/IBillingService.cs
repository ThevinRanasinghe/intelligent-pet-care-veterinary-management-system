using PetCare.Application.DTOs.Billing;

namespace PetCare.Application.Interfaces;

public interface IBillingService
{
    Task<IReadOnlyList<QuotationResponseDto>> GetQuotationsAsync(
        Guid organizationId,
        CancellationToken ct = default);

    Task<QuotationResponseDto?> GetQuotationByIdAsync(
        Guid id,
        Guid organizationId,
        CancellationToken ct = default);

    Task<QuotationResponseDto> CreateQuotationAsync(
        Guid organizationId,
        CreateQuotationDto request,
        CancellationToken ct = default);

    Task<QuotationResponseDto> UpdateQuotationAsync(
        Guid id,
        Guid organizationId,
        UpdateQuotationDto request,
        CancellationToken ct = default);

    Task<QuotationResponseDto> CalculateQuotationAsync(
        Guid id,
        Guid organizationId,
        CancellationToken ct = default);

    Task<QuotationResponseDto> SubmitQuotationForApprovalAsync(
        Guid id,
        Guid organizationId,
        CancellationToken ct = default);
}
