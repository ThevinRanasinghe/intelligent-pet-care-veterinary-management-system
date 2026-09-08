using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PetCare.Api.DTOs;
using PetCare.Application.DTOs.Auth;
using PetCare.Application.Interfaces;

namespace PetCare.Api.Controllers;

/// <summary>
/// Authentication endpoints — thin controller that delegates to IAuthService.
/// No password hashing, JWT construction, or SQL queries here.
/// </summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService                          _authService;
    private readonly IValidator<LoginRequestDto>           _loginValidator;
    private readonly IValidator<RegisterPetOwnerRequestDto> _registerValidator;

    public AuthController(
        IAuthService                          authService,
        IValidator<LoginRequestDto>           loginValidator,
        IValidator<RegisterPetOwnerRequestDto> registerValidator)
    {
        _authService       = authService;
        _loginValidator    = loginValidator;
        _registerValidator = registerValidator;
    }

    /// <summary>
    /// Authenticate a user and receive a JWT access token.
    /// </summary>
    /// <response code="200">Returns the JWT and user information.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="401">Invalid email or password.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequestDto request,
        CancellationToken ct)
    {
        var validation = await _loginValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new ValidationErrorResponse(
                false,
                "Validation failed.",
                validation.ToDictionary()));

        var response = await _authService.LoginAsync(request, ct);
        return Ok(ApiResponse<LoginResponseDto>.Ok(response, "Login successful."));
    }

    /// <summary>
    /// Register a new PetOwner account. Role is always PetOwner — cannot be overridden.
    /// </summary>
    /// <response code="201">Registration successful.</response>
    /// <response code="400">Validation error or duplicate email.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<CurrentUserDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterPetOwnerRequestDto request,
        CancellationToken ct)
    {
        var validation = await _registerValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return BadRequest(new ValidationErrorResponse(
                false,
                "Validation failed.",
                validation.ToDictionary()));

        var user = await _authService.RegisterPetOwnerAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created,
            ApiResponse<CurrentUserDto>.Ok(user, "Registration successful. Please log in."));
    }

    /// <summary>
    /// Returns the currently authenticated user's profile.
    /// Requires a valid Bearer JWT.
    /// </summary>
    /// <response code="200">Returns current user information.</response>
    /// <response code="401">No token or invalid token.</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<CurrentUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userId))
            return Unauthorized(ApiResponse.Fail("User identity not found."));

        var user = await _authService.GetCurrentUserAsync(userId, ct);
        if (user is null)
            return Unauthorized(ApiResponse.Fail("User not found or account is inactive."));

        return Ok(ApiResponse<CurrentUserDto>.Ok(user));
    }
}
