using PetCare.Application.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PetCare.Tests;

/// <summary>
/// Unscoped tenant context for unit tests: behaves like a platform admin /
/// system caller, so organization filtering is a no-op.
/// </summary>
public sealed class TestTenantContext : ITenantContext
{
    public static readonly TestTenantContext Unscoped = new();

    public Guid? UserId => null;
    public bool IsPlatformAdmin => true;
    public bool IsOrganizationScoped => false;
    public Task<Guid?> GetOrganizationIdAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<Guid?>(null);
}
