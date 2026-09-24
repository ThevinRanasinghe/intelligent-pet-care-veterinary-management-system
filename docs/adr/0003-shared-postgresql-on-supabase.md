# ADR 0003 — Shared PostgreSQL on Supabase

**Status:** Accepted · **Date:** September 2026

## Context

The assignment requires React and Flutter to share one ASP.NET Core API and one PostgreSQL database. The team needed a hosted, always-available Postgres all members could reach without local DB parity issues.

## Options considered

- **Supabase hosted PostgreSQL** — managed PG, pooled connection endpoint, free tier
- Local PostgreSQL per developer — divergent data/schema risk
- Neon / Railway / self-hosted — comparable; Supabase already provisioned by the team

## Decision

Shared Supabase PostgreSQL (`aws-0-ap-southeast-2.pooler.supabase.com`, database `postgres`). Schema owned exclusively by EF Core migrations — `InitialSchedulingBillingApproval` → `AddUsers` → `ConsolidatedDomainModel` are applied. `EnsureCreated()` is never used.

## Consequences

- One schema of record; every member's app sees identical data
- Connection string is a secret delivered via `ConnectionStrings:PetCareDb` user secret or `PETCARE_DB_CONNECTION` — never committed
- `dotnet ef` tooling resolves its connection only via `PETCARE_DB_CONNECTION` (design-time factory ignores user secrets) — documented in the setup guide
