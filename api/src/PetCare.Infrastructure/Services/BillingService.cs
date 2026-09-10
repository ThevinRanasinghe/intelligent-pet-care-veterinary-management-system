using Microsoft.EntityFrameworkCore;
using PetCare.Application.DTOs.Billing;
using PetCare.Application.Interfaces;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;
using PetCare.Infrastructure.Persistence;

namespace PetCare.Infrastructure.Services;

public sealed class BillingService : IBillingService
{
    private readonly PetCareDbContext _dbContext;

    public BillingService(PetCareDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<QuotationResponseDto>> GetQuotationsAsync(
        Guid organizationId,
        CancellationToken ct = default)
    {
        var quotations = await _dbContext.Quotations
            .Include(q => q.Appointment)
            .Include(q => q.Items)
            .AsNoTracking()
            .Where(q => q.Appointment.OrganizationId == organizationId)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync(ct);

        return quotations.Select(MapToResponse).ToList();
    }

    /// <inheritdoc />
    public async Task<QuotationResponseDto?> GetQuotationByIdAsync(
        Guid id,
        Guid organizationId,
        CancellationToken ct = default)
    {
        var q = await _dbContext.Quotations
            .Include(q => q.Appointment)
            .Include(q => q.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == id && q.Appointment.OrganizationId == organizationId, ct);

        return q is null ? null : MapToResponse(q);
    }

    /// <inheritdoc />
    public async Task<QuotationResponseDto> CreateQuotationAsync(
        Guid organizationId,
        CreateQuotationDto request,
        CancellationToken ct = default)
    {
        if (request.Budget < 0)
            throw new InvalidOperationException("Budget must be non-negative.");

        var appt = await _dbContext.Appointments
            .FirstOrDefaultAsync(a => a.Id == request.AppointmentId && a.OrganizationId == organizationId, ct);

        if (appt is null)
            throw new KeyNotFoundException("Appointment not found in your organization.");

        var existingQuotation = await _dbContext.Quotations
            .AnyAsync(q => q.AppointmentId == request.AppointmentId, ct);

        if (existingQuotation)
            throw new InvalidOperationException("A quotation already exists for this appointment.");

        var quotation = new Quotation
        {
            Id = Guid.NewGuid(),
            AppointmentId = request.AppointmentId,
            Budget = request.Budget,
            Status = QuotationStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        decimal subtotal = 0m;
        foreach (var itemReq in request.Items)
        {
            if (itemReq.Quantity <= 0) throw new InvalidOperationException("Item quantity must be greater than zero.");
            if (itemReq.UnitPrice < 0) throw new InvalidOperationException("Item unit price must be non-negative.");

            var totalPrice = itemReq.Quantity * itemReq.UnitPrice;
            subtotal += totalPrice;

            quotation.Items.Add(new QuotationItem
            {
                Id = Guid.NewGuid(),
                QuotationId = quotation.Id,
                Category = itemReq.Category.Trim(),
                Description = itemReq.Description.Trim(),
                Quantity = itemReq.Quantity,
                UnitPrice = itemReq.UnitPrice,
                TotalPrice = totalPrice
            });
        }

        quotation.Subtotal = subtotal;
        quotation.Total = subtotal;

        _dbContext.Quotations.Add(quotation);
        await _dbContext.SaveChangesAsync(ct);

        return (await GetQuotationByIdAsync(quotation.Id, organizationId, ct))!;
    }

    /// <inheritdoc />
    public async Task<QuotationResponseDto> UpdateQuotationAsync(
        Guid id,
        Guid organizationId,
        UpdateQuotationDto request,
        CancellationToken ct = default)
    {
        var quotation = await _dbContext.Quotations
            .Include(q => q.Appointment)
            .Include(q => q.Items)
            .FirstOrDefaultAsync(q => q.Id == id && q.Appointment.OrganizationId == organizationId, ct);

        if (quotation is null)
            throw new KeyNotFoundException("Quotation not found in your organization.");

        if (quotation.Status is QuotationStatus.Approved or QuotationStatus.Finalised)
            throw new InvalidOperationException("Approved or Finalised quotations cannot be modified.");

        if (request.Budget < 0)
            throw new InvalidOperationException("Budget must be non-negative.");

        quotation.Budget = request.Budget;

        // Clear existing items and re-add
        _dbContext.QuotationItems.RemoveRange(quotation.Items);
        quotation.Items.Clear();

        decimal subtotal = 0m;
        foreach (var itemReq in request.Items)
        {
            if (itemReq.Quantity <= 0) throw new InvalidOperationException("Item quantity must be greater than zero.");
            if (itemReq.UnitPrice < 0) throw new InvalidOperationException("Item unit price must be non-negative.");

            var totalPrice = itemReq.Quantity * itemReq.UnitPrice;
            subtotal += totalPrice;

            quotation.Items.Add(new QuotationItem
            {
                Id = Guid.NewGuid(),
                QuotationId = quotation.Id,
                Category = itemReq.Category.Trim(),
                Description = itemReq.Description.Trim(),
                Quantity = itemReq.Quantity,
                UnitPrice = itemReq.UnitPrice,
                TotalPrice = totalPrice
            });
        }

        quotation.Subtotal = subtotal;
        quotation.Total = subtotal;
        quotation.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        return (await GetQuotationByIdAsync(id, organizationId, ct))!;
    }

    /// <inheritdoc />
    public async Task<QuotationResponseDto> CalculateQuotationAsync(
        Guid id,
        Guid organizationId,
        CancellationToken ct = default)
    {
        var quotation = await _dbContext.Quotations
            .Include(q => q.Appointment)
            .Include(q => q.Items)
            .FirstOrDefaultAsync(q => q.Id == id && q.Appointment.OrganizationId == organizationId, ct);

        if (quotation is null)
            throw new KeyNotFoundException("Quotation not found in your organization.");

        decimal sum = quotation.Items.Sum(i => i.TotalPrice);
        quotation.Subtotal = sum;
        quotation.Total = sum;
        quotation.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        return MapToResponse(quotation);
    }

    /// <inheritdoc />
    public async Task<QuotationResponseDto> SubmitQuotationForApprovalAsync(
        Guid id,
        Guid organizationId,
        CancellationToken ct = default)
    {
        var quotation = await _dbContext.Quotations
            .Include(q => q.Appointment)
            .Include(q => q.Items)
            .Include(q => q.Approval)
            .FirstOrDefaultAsync(q => q.Id == id && q.Appointment.OrganizationId == organizationId, ct);

        if (quotation is null)
            throw new KeyNotFoundException("Quotation not found in your organization.");

        if (quotation.Total > quotation.Budget)
            throw new InvalidOperationException($"Quotation total ({quotation.Total}) exceeds the stated budget ({quotation.Budget}).");

        quotation.Status = QuotationStatus.PendingApproval;
        quotation.UpdatedAt = DateTimeOffset.UtcNow;

        if (quotation.Approval is null)
        {
            var approval = new Approval
            {
                Id = Guid.NewGuid(),
                QuotationId = quotation.Id,
                Status = ApprovalStatus.Pending
            };
            _dbContext.Approvals.Add(approval);
        }
        else
        {
            quotation.Approval.Status = ApprovalStatus.Pending;
        }

        await _dbContext.SaveChangesAsync(ct);
        return MapToResponse(quotation);
    }

    private static QuotationResponseDto MapToResponse(Quotation q) =>
        new(
            Id: q.Id,
            AppointmentId: q.AppointmentId,
            Budget: q.Budget,
            Subtotal: q.Subtotal,
            Total: q.Total,
            Status: q.Status.ToString(),
            CreatedAt: q.CreatedAt,
            UpdatedAt: q.UpdatedAt,
            Items: q.Items.Select(i => new QuotationItemDto(
                Id: i.Id,
                Category: i.Category,
                Description: i.Description,
                Quantity: i.Quantity,
                UnitPrice: i.UnitPrice,
                TotalPrice: i.TotalPrice
            )).ToList()
        );
}
