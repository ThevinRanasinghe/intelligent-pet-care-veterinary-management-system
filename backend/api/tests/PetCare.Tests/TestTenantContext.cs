using PetCare.Application.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PetCare.Tests;

/// <summary>
/// Unscoped tenant context for unit tests: behaves like a platform admin /
/// system caller, so organization filtering is a no-op. Properties are
/// settable so tests can also emulate a signed-in caller with a role
/// (e.g. a Veterinarian user with a linked profile).
/// </summary>
public sealed class TestTenantContext : ITenantContext
{
    public static readonly TestTenantContext Unscoped = new();

    public Guid? UserId { get; set; }

    /// <summary>Role the fake caller is in (matched by IsInRole).</summary>
    public string? Role { get; set; }

    public bool IsPlatformAdmin { get; set; } = true;

    public bool IsOrganizationScoped { get; set; }

    public Guid? OrganizationId { get; set; }

    public bool IsInRole(string role) => Role == role;

    public Task<Guid?> GetOrganizationIdAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(OrganizationId);
}
