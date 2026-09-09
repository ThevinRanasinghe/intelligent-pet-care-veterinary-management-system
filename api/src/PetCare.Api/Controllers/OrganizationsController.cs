using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetCare.Api.DTOs;
using PetCare.Application.DTOs.Organizations;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;
using PetCare.Infrastructure.Entities;
using PetCare.Infrastructure.Persistence;

namespace PetCare.Api.Controllers;

/// <summary>
/// Platform Organization Management endpoints.
/// Provides full lifecycle management for Veterinary Organizations on Beacon Pet Health:
/// - SuperAdmin: Review, Approve, Reject, Suspend, Activate, and List all organizations.
/// - ClinicManager: View and edit own organization workspace with strict tenant isolation.
/// </summary>
[ApiController]
[Route("api/organizations")]
[Produces("application/json")]
[Authorize]
public sealed class OrganizationsController : ControllerBase
{
    private readonly PetCareDbContext            _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUserService         _currentUser;

    public OrganizationsController(
        PetCareDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ICurrentUserService currentUser)
    {
        _dbContext   = dbContext;
        _userManager = userManager;
        _currentUser = currentUser;
    }

    /// <summary>
    /// READ ALL: Lists all registered veterinary organizations.
    /// Restricted to SuperAdmin platform administrators.
    /// Supports filtering by search term and lifecycle status.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<OrganizationDetailsDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] OrganizationStatus? status,
        CancellationToken ct)
    {
        var query = _dbContext.Organizations.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(o => o.Name.ToLower().Contains(term)
                                  || o.Email.ToLower().Contains(term)
                                  || o.City.ToLower().Contains(term));
        }

        if (status.HasValue)
        {
            query = query.Where(o => o.Status == status.Value);
        }

        var organizations = await query
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(ct);

        var orgIds = organizations.Select(o => o.Id).ToList();

        // Compute staff counts
        var staffCounts = await _dbContext.Users
            .Where(u => u.OrganizationId.HasValue && orgIds.Contains(u.OrganizationId.Value))
            .GroupBy(u => u.OrganizationId!.Value)
            .Select(g => new { OrgId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OrgId, x => x.Count, ct);

        // Fetch primary/initial manager for each organization
        var managers = await _dbContext.Users
            .Where(u => u.OrganizationId.HasValue && orgIds.Contains(u.OrganizationId.Value))
            .OrderBy(u => u.CreatedAt)
            .GroupBy(u => u.OrganizationId!.Value)
            .Select(g => new
            {
                OrgId = g.Key,
                Manager = g.Select(u => new { u.FullName, u.Email }).FirstOrDefault()
            })
            .ToDictionaryAsync(x => x.OrgId, x => x.Manager, ct);

        var dtos = organizations.Select(o =>
        {
            var manager = managers.GetValueOrDefault(o.Id);
            return MapToDetailsDto(
                o,
                staffCounts.GetValueOrDefault(o.Id, 0),
                manager?.FullName,
                manager?.Email
            );
        });

        return Ok(ApiResponse<IEnumerable<OrganizationDetailsDto>>.Ok(dtos));
    }

    /// <summary>
    /// READ ONE: Gets details of a specific organization.
    /// SuperAdmin can view any; ClinicManager can only view their own organization.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.ClinicManager}")]
    [ProducesResponseType(typeof(ApiResponse<OrganizationDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        // Tenant isolation: ClinicManager cannot inspect another organization
        if (!_currentUser.IsInRole(Roles.SuperAdmin) && _currentUser.OrganizationId != id)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse.Fail("Access denied: You can only access your own organization."));
        }

        var org = await _dbContext.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, ct);

        if (org is null)
        {
            return NotFound(ApiResponse.Fail("Organization not found."));
        }

        var staffCount = await _dbContext.Users.CountAsync(u => u.OrganizationId == id, ct);
        var manager = await _dbContext.Users
            .Where(u => u.OrganizationId == id)
            .OrderBy(u => u.CreatedAt)
            .Select(u => new { u.FullName, u.Email })
            .FirstOrDefaultAsync(ct);

        return Ok(ApiResponse<OrganizationDetailsDto>.Ok(
            MapToDetailsDto(org, staffCount, manager?.FullName, manager?.Email)));
    }

    /// <summary>
    /// APPROVE: SuperAdmin approves a pending veterinary organization.
    /// Atomically activates the Organization and the primary ClinicManager account.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(typeof(ApiResponse<OrganizationDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        var org = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (org is null)
        {
            return NotFound(ApiResponse.Fail("Organization not found."));
        }

        if (org.Status == OrganizationStatus.Active)
        {
            return BadRequest(ApiResponse.Fail("Organization is already active."));
        }

        org.Status            = OrganizationStatus.Active;
        org.IsActive          = true;
        org.ApprovedAt        = DateTime.UtcNow;
        org.ApprovedByUserId  = _currentUser.UserId;
        org.UpdatedAt         = DateTime.UtcNow;

        // Activate the organization's initial ClinicManager
        var managers = await _dbContext.Users
            .Where(u => u.OrganizationId == id)
            .ToListAsync(ct);

        foreach (var mgr in managers)
        {
            mgr.AccountStatus = UserAccountStatus.Active;
            mgr.IsActive      = true;
            mgr.UpdatedAt     = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(ct);

        var staffCount = managers.Count;
        var primaryMgr = managers.OrderBy(m => m.CreatedAt).FirstOrDefault();

        return Ok(ApiResponse<OrganizationDetailsDto>.Ok(
            MapToDetailsDto(org, staffCount, primaryMgr?.FullName, primaryMgr?.Email),
            "Veterinary organization and initial Clinic Manager approved successfully."));
    }

    /// <summary>
    /// REJECT: SuperAdmin rejects a pending veterinary organization registration.
    /// Records the rejection reason and prevents manager from operating.
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(typeof(ApiResponse<OrganizationDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reject(
        Guid id,
        [FromBody] RejectOrganizationDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequest(ApiResponse.Fail("A rejection reason is required."));
        }

        var org = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (org is null)
        {
            return NotFound(ApiResponse.Fail("Organization not found."));
        }

        org.Status            = OrganizationStatus.Rejected;
        org.IsActive          = false;
        org.RejectedAt        = DateTime.UtcNow;
        org.RejectedByUserId  = _currentUser.UserId;
        org.RejectionReason   = request.Reason.Trim();
        org.UpdatedAt         = DateTime.UtcNow;

        // Keep staff accounts inactive
        var staff = await _dbContext.Users
            .Where(u => u.OrganizationId == id)
            .ToListAsync(ct);

        foreach (var member in staff)
        {
            member.AccountStatus = UserAccountStatus.Suspended;
            member.IsActive      = false;
            member.UpdatedAt     = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(ct);

        var primaryMgr = staff.OrderBy(m => m.CreatedAt).FirstOrDefault();

        return Ok(ApiResponse<OrganizationDetailsDto>.Ok(
            MapToDetailsDto(org, staff.Count, primaryMgr?.FullName, primaryMgr?.Email),
            "Organization registration rejected."));
    }

    /// <summary>
    /// SUSPEND: SuperAdmin suspends an active veterinary organization.
    /// Blocks staff from accessing organization functionality while preserving audit data.
    /// </summary>
    [HttpPost("{id:guid}/suspend")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(typeof(ApiResponse<OrganizationDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Suspend(Guid id, CancellationToken ct)
    {
        var org = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (org is null)
        {
            return NotFound(ApiResponse.Fail("Organization not found."));
        }

        org.Status    = OrganizationStatus.Suspended;
        org.IsActive  = false;
        org.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        var staffCount = await _dbContext.Users.CountAsync(u => u.OrganizationId == id, ct);
        return Ok(ApiResponse<OrganizationDetailsDto>.Ok(
            MapToDetailsDto(org, staffCount), "Organization has been suspended."));
    }

    /// <summary>
    /// ACTIVATE: SuperAdmin reactivates a suspended organization.
    /// </summary>
    [HttpPost("{id:guid}/activate")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(typeof(ApiResponse<OrganizationDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var org = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (org is null)
        {
            return NotFound(ApiResponse.Fail("Organization not found."));
        }

        org.Status    = OrganizationStatus.Active;
        org.IsActive  = true;
        org.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        var staffCount = await _dbContext.Users.CountAsync(u => u.OrganizationId == id, ct);
        return Ok(ApiResponse<OrganizationDetailsDto>.Ok(
            MapToDetailsDto(org, staffCount), "Organization reactivated successfully."));
    }

    /// <summary>
    /// UPDATE: Updates organization details.
    /// SuperAdmin can update any; ClinicManager can update their own organization.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{Roles.SuperAdmin},{Roles.ClinicManager}")]
    [ProducesResponseType(typeof(ApiResponse<OrganizationDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOrganizationDto request, CancellationToken ct)
    {
        // Tenant isolation
        if (!_currentUser.IsInRole(Roles.SuperAdmin) && _currentUser.OrganizationId != id)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse.Fail("Access denied: You can only update your own organization."));
        }

        var org = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (org is null)
        {
            return NotFound(ApiResponse.Fail("Organization not found."));
        }

        // Verify email uniqueness if changed
        if (!string.Equals(org.Email, request.Email, StringComparison.OrdinalIgnoreCase))
        {
            var emailExists = await _dbContext.Organizations.AnyAsync(o => o.Id != id && o.Email.ToLower() == request.Email.ToLower(), ct);
            if (emailExists)
            {
                return BadRequest(ApiResponse.Fail("Organization email is already used by another organization."));
            }
        }

        org.Name               = request.Name.Trim();
        org.RegistrationNumber = string.IsNullOrWhiteSpace(request.RegistrationNumber) ? null : request.RegistrationNumber.Trim();
        org.Email              = request.Email.Trim();
        org.Phone              = request.Phone.Trim();
        org.Address            = request.Address.Trim();
        org.City               = request.City.Trim();
        org.Country            = request.Country.Trim();
        org.UpdatedAt          = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        var staffCount = await _dbContext.Users.CountAsync(u => u.OrganizationId == id, ct);
        return Ok(ApiResponse<OrganizationDetailsDto>.Ok(MapToDetailsDto(org, staffCount), "Organization updated successfully."));
    }

    /// <summary>
    /// DELETE / DEACTIVATE: Soft-deactivates an organization.
    /// Restricted to SuperAdmin platform administrators.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var org = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (org is null)
        {
            return NotFound(ApiResponse.Fail("Organization not found."));
        }

        org.IsActive  = false;
        org.Status    = OrganizationStatus.Inactive;
        org.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        return Ok(ApiResponse.Ok("Organization deactivated successfully."));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helper
    // ─────────────────────────────────────────────────────────────────────────

    private static OrganizationDetailsDto MapToDetailsDto(
        Organization o,
        int staffCount,
        string? managerName = null,
        string? managerEmail = null) =>
        new(
            Id:                 o.Id,
            Name:               o.Name,
            RegistrationNumber: o.RegistrationNumber,
            Email:              o.Email,
            Phone:              o.Phone,
            Address:            o.Address,
            City:               o.City,
            Country:            o.Country,
            Status:             o.Status.ToString(),
            IsActive:           o.IsActive,
            CreatedAt:          o.CreatedAt,
            UpdatedAt:          o.UpdatedAt,
            StaffCount:         staffCount,
            ApprovedAt:         o.ApprovedAt,
            ApprovedByUserId:   o.ApprovedByUserId,
            RejectedAt:         o.RejectedAt,
            RejectedByUserId:   o.RejectedByUserId,
            RejectionReason:    o.RejectionReason,
            InitialManagerName: managerName,
            InitialManagerEmail: managerEmail
        );
}
