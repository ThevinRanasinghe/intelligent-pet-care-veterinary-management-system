using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetCare.Api.DTOs;
using PetCare.Application.DTOs.Auth;
using PetCare.Application.DTOs.Users;
using PetCare.Application.Interfaces;
using PetCare.Infrastructure.Entities;
using PetCare.Infrastructure.Persistence;

namespace PetCare.Api.Controllers;

/// <summary>
/// User Profile and Account Management endpoints for authenticated users.
/// </summary>
[ApiController]
[Route("api/account")]
[Produces("application/json")]
[Authorize]
public sealed class AccountController : ControllerBase
{
    private readonly UserManager<ApplicationUser>            _userManager;
    private readonly PetCareDbContext                       _dbContext;
    private readonly ICurrentUserService                    _currentUser;
    private readonly IValidator<ChangePasswordRequestDto>   _passwordValidator;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        PetCareDbContext dbContext,
        ICurrentUserService currentUser,
        IValidator<ChangePasswordRequestDto> passwordValidator)
    {
        _userManager       = userManager;
        _dbContext         = dbContext;
        _currentUser       = currentUser;
        _passwordValidator = passwordValidator;
    }

    /// <summary>
    /// GET: Retrieves the current authenticated user's profile and organization details.
    /// </summary>
    [HttpGet("profile")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(ApiResponse.Fail("User identity not found."));
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Unauthorized(ApiResponse.Fail("User account not found."));
        }

        var roles = await _userManager.GetRolesAsync(user);
        var role  = roles.FirstOrDefault() ?? "User";

        OrganizationDto? orgDto = null;
        if (user.OrganizationId.HasValue)
        {
            var org = await _dbContext.Organizations
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == user.OrganizationId.Value, ct);

            if (org is not null)
            {
                orgDto = new OrganizationDto(
                    Id:     org.Id,
                    Name:   org.Name,
                    Email:  org.Email,
                    Phone:  org.Phone,
                    Status: org.Status.ToString()
                );
            }
        }

        var profile = new UserProfileDto(
            Id:           user.Id,
            FirstName:    user.FirstName,
            LastName:     user.LastName,
            FullName:     user.FullName,
            Email:        user.Email ?? string.Empty,
            PhoneNumber:  user.PhoneNumber,
            Role:         role,
            Status:       user.AccountStatus.ToString(),
            Organization: orgDto,
            CreatedAt:    user.CreatedAt,
            LastLoginAt:  user.LastLoginAt
        );

        return Ok(ApiResponse<UserProfileDto>.Ok(profile));
    }

    /// <summary>
    /// PUT: Updates authenticated user's profile information.
    /// Role, Organization, and AccountStatus can NEVER be altered through this endpoint.
    /// </summary>
    [HttpPut("profile")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateProfileRequestDto request,
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(ApiResponse.Fail("User identity not found."));
        }

        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
        {
            return BadRequest(ApiResponse.Fail("First name and last name are required."));
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Unauthorized(ApiResponse.Fail("User account not found."));
        }

        user.FirstName   = request.FirstName.Trim();
        user.LastName    = request.LastName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        user.UpdatedAt   = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return BadRequest(ApiResponse.Fail(errors));
        }

        return await GetProfile(ct);
    }

    /// <summary>
    /// POST: Changes password for the currently authenticated user.
    /// </summary>
    [HttpPost("change-password")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequestDto request,
        CancellationToken ct)
    {
        var validation = await _passwordValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return BadRequest(new ValidationErrorResponse(
                false,
                "Validation failed.",
                validation.ToDictionary()));
        }

        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(ApiResponse.Fail("User identity not found."));
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Unauthorized(ApiResponse.Fail("User account not found."));
        }

        var result = await _userManager.ChangePasswordAsync(
            user, request.CurrentPassword, request.NewPassword);

        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            return BadRequest(ApiResponse.Fail(errors));
        }

        return Ok(ApiResponse.Ok("Password changed successfully."));
    }
}
