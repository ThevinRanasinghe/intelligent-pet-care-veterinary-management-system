using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetCare.Api.DTOs;
using PetCare.Application.DTOs.Organizations;
using PetCare.Application.Interfaces;
using PetCare.Domain.Constants;
using PetCare.Domain.Entities;
using PetCare.Domain.Enums;
using PetCare.Infrastructure.Persistence;

namespace PetCare.Api.Controllers;

/// <summary>
/// Platform Organization Management endpoints.
/// Provides full CRUD operations for Veterinary Organizations on Beacon Pet Health.
/// Access is restricted to SuperAdmin (with self-management for Clinic Managers).
/// </summary>
[ApiController]
[Route("api/organizations")]
[Produces("application/json")]
[Authorize]
public sealed class OrganizationsController : ControllerBase
{
    private readonly PetCareDbContext    _dbContext;
    private readonly ICurrentUserService _currentUser;

    public OrganizationsController(PetCareDbContext dbContext, ICurrentUserService currentUser)
    {
        _dbContext   = dbContext;
        _currentUser = currentUser;
    }

    /// <summary>
    /// READ ALL: Lists all registered veterinary organizations.
    /// Restricted to SuperAdmin platform administrators.
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

        // Compute staff counts
        var orgIds = organizations.Select(o => o.Id).ToList();
        var staffCounts = await _dbContext.Users
            .Where(u => u.OrganizationId.HasValue && orgIds.Contains(u.OrganizationId.Value))
            .GroupBy(u => u.OrganizationId!.Value)
            .Select(g => new { OrgId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OrgId, x => x.Count, ct);

        var dtos = organizations.Select(o => MapToDetailsDto(o, staffCounts.GetValueOrDefault(o.Id, 0)));

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

        return Ok(ApiResponse<OrganizationDetailsDto>.Ok(MapToDetailsDto(org, staffCount)));
    }

    /// <summary>
    /// CREATE: Creates a new organization workspace.
    /// Restricted to SuperAdmin.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(typeof(ApiResponse<OrganizationDetailsDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateOrganizationDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(ApiResponse.Fail("Organization name and email are required."));
        }

        var exists = await _dbContext.Organizations
            .AnyAsync(o => o.Name.ToLower() == request.Name.ToLower()
                        || o.Email.ToLower() == request.Email.ToLower(), ct);

        if (exists)
        {
            return BadRequest(ApiResponse.Fail("An organization with this name or email already exists."));
        }

        var org = new Organization
        {
            Id                 = Guid.NewGuid(),
            Name               = request.Name.Trim(),
            RegistrationNumber = string.IsNullOrWhiteSpace(request.RegistrationNumber) ? null : request.RegistrationNumber.Trim(),
            Email              = request.Email.Trim(),
            Phone              = request.Phone.Trim(),
            Address            = request.Address.Trim(),
            City               = request.City.Trim(),
            Country            = request.Country.Trim(),
            Status             = request.Status,
            IsActive           = request.Status == OrganizationStatus.Active,
            CreatedAt          = DateTime.UtcNow,
            UpdatedAt          = DateTime.UtcNow
        };

        _dbContext.Organizations.Add(org);
        await _dbContext.SaveChangesAsync(ct);

        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<OrganizationDetailsDto>.Ok(MapToDetailsDto(org, 0), "Organization created successfully."));
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
    /// UPDATE STATUS: Approves, activates, suspends, or deactivates an organization.
    /// Restricted to SuperAdmin platform administrators.
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(typeof(ApiResponse<OrganizationDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateOrganizationStatusDto request, CancellationToken ct)
    {
        var org = await _dbContext.Organizations.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (org is null)
        {
            return NotFound(ApiResponse.Fail("Organization not found."));
        }

        org.Status    = request.Status;
        org.IsActive  = request.Status == OrganizationStatus.Active;
        org.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        var staffCount = await _dbContext.Users.CountAsync(u => u.OrganizationId == id, ct);
        return Ok(ApiResponse<OrganizationDetailsDto>.Ok(MapToDetailsDto(org, staffCount),
            $"Organization status updated to {request.Status}."));
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

        // Soft delete / set Inactive
        org.IsActive  = false;
        org.Status    = OrganizationStatus.Inactive;
        org.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        return Ok(ApiResponse.Ok("Organization deactivated successfully."));
    }

    // ── Helper ──
    private static OrganizationDetailsDto MapToDetailsDto(Organization o, int staffCount) =>
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
            StaffCount:         staffCount
        );
}
