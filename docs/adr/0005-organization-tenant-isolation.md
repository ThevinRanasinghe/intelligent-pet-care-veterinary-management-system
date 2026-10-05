# ADR 0005 — Organization Tenant Isolation

**Status:** Accepted · **Date:** September 2026 (pre-migration corrections)

## Context

Multiple veterinary organizations share one database. Staff of org A must never see org B's data; PetOwners are not org-scoped (ownership rules instead); Administrator is platform-wide.

## Options considered

- **`OrganizationId` columns + centralized query scoping** — direct columns on `Veterinarian`/`Medicine`/`Supplier`/`Users`, transitive scope elsewhere
- Row-level security (Postgres RLS) — more enforcement depth, but EF tooling/testing complexity
- Separate schemas/databases per org — operationally heavy for this scope

## Decision

Tenant scoping via `ITenantContext` (resolves caller's org from JWT `sub` → `Users.OrganizationId`, once per request) + `TenantQueryableExtensions.ScopeToOrganizationAsync` applied to every org-owned repository query. Transitive resolution: `Appointment`/`Slot` → Veterinarian; `Quotation`/`Approval`/`History` → Appointment.Veterinarian; `Batch`/`Reservation`/`Transaction` → Medicine; clinical chain → Examination.Veterinarian. Cross-org ids on create fail as 404. `NULL`-org rows are invisible to scoped staff.

## Consequences

- Single enforcement point (`ITenantContext`) — easy to audit
- NULL-org backfill is a deliberate consequence: unassigned legacy data is hidden rather than leaked
- RLS remains a possible defense-in-depth addition later; not required now
