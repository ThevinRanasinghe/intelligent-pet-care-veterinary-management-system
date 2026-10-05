# ADR 0004 — Layered Backend Architecture

**Status:** Accepted · **Date:** 2026 (pre-existing, ratified)

## Context

The backend needed a structure that keeps HTTP concerns, business rules, domain model, and persistence separable and unit-testable.

## Options considered

- **Layered (Api → Application → Domain ← Infrastructure)** — interface-based, mockable
- Minimal APIs / single-project — faster to start, weaker separation for a multi-module domain

## Decision

Four projects:

- `PetCare.Api` — controllers, middleware, DI composition root
- `PetCare.Application` — services, DTOs, FluentValidation validators, repository interfaces (no EF reference)
- `PetCare.Domain` — entities, enums, `Roles`/`SeedIds` constants
- `PetCare.Infrastructure` — EF Core `PetCareDbContext`, configurations, repositories, `PasswordHasher`, `JwtTokenGenerator`, seeding

Rules enforced by construction: controllers never touch EF; Application never references Infrastructure; ownership/tenancy resolved via `IOwnerAccessService`/`ITenantContext` inside services, not controllers.

## Consequences

- 107 Application-layer unit tests run with pure Moq — no database needed
- Adding a module = entity + configuration + repository + service + controller + validator; convention is uniform across all modules
