using Microsoft.AspNetCore.Mvc;
using PetCare.Application.DTOs.Auth;
using PetCare.Application.Interfaces;

namespace PetCare.Api.Controllers;

/// <summary>
/// Shared authentication endpoints. All business rules (credential
/// validation, JWT issuance) are enforced by <see cref="IAuthService"/> in
/// PetCare.Application; this controller only handles HTTP concerns.
/// </summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Validates credentials and returns a signed JWT (with user id and
    /// role claims) plus basic profile info. Never returns the
    /// password/password hash. Returns 401 for invalid credentials.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var response = await _authService.LoginAsync(request, cancellationToken);
        return Ok(response);
    }
}
