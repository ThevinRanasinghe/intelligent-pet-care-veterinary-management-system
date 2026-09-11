using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using PetCare.Application.Interfaces;

namespace PetCare.Infrastructure.Services;

/// <summary>
/// Retrieves authenticated user details and organization context from the current HTTP request.
/// Critical for multi-tenant data isolation — guarantees that organization context is extracted
/// server-side from cryptographically signed JWT claims, never from client input.
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    /// <inheritdoc />
    public string? UserId =>
        User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User?.FindFirstValue("sub");

    /// <inheritdoc />
    public string? Email =>
        User?.FindFirstValue(ClaimTypes.Email)
        ?? User?.FindFirstValue("email");

    /// <inheritdoc />
    public string? Role =>
        User?.FindFirstValue(ClaimTypes.Role)
        ?? User?.FindFirstValue("role");

    /// <inheritdoc />
    public Guid? OrganizationId
    {
        get
        {
            var claimValue = User?.FindFirstValue("organizationId");
            if (!string.IsNullOrEmpty(claimValue) && Guid.TryParse(claimValue, out var orgId))
            {
                return orgId;
            }
            return null;
        }
    }

    /// <inheritdoc />
    public bool IsAuthenticated =>
        User?.Identity?.IsAuthenticated ?? false;

    /// <inheritdoc />
    public bool IsInRole(string role) =>
        User?.IsInRole(role) ?? false;
}
