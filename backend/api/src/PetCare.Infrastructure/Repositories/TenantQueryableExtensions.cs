using System.Linq.Expressions;
using PetCare.Application.Interfaces;

namespace PetCare.Infrastructure.Repositories;

/// <summary>
/// Applies organization filtering to queries over org-owned entities. When
/// the caller is organization-scoped, the query is restricted to rows whose
/// (possibly transitive) OrganizationId equals the caller's organization.
/// Rows with a null OrganizationId are unassigned — they match a scoped
/// caller whose own organization is null, and are otherwise treated as
/// shared/unowned data (they never equal another organization's id).
/// </summary>
internal static class TenantQueryableExtensions
{
    public static async Task<IQueryable<TEntity>> ScopeToOrganizationAsync<TEntity>(
        this IQueryable<TEntity> query,
        ITenantContext tenant,
        Expression<Func<TEntity, Guid?>> organizationIdSelector,
        CancellationToken cancellationToken = default)
    {
        if (!tenant.IsOrganizationScoped)
        {
            return query;
        }

        var organizationId = await tenant.GetOrganizationIdAsync(cancellationToken);
        var parameter = organizationIdSelector.Parameters[0];
        var body = Expression.Equal(
            organizationIdSelector.Body,
            Expression.Constant(organizationId, typeof(Guid?)));

        return query.Where(Expression.Lambda<Func<TEntity, bool>>(body, parameter));
    }
}
