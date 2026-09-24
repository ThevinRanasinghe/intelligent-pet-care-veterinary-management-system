# Deployment Guide

Intended deployment architecture for PetCare AI. **Status: not yet deployed** — everything below documents the target topology and configuration; live URLs are placeholders pending the actual deployment.

## Current deployment status

| Component | Status | URL |
|---|---|---|
| ASP.NET Core API | [TO BE DEPLOYED] | Health: `[HEALTH URL]` · Swagger: `[SWAGGER URL]` |
| React web | [TO BE DEPLOYED] | `[LIVE URL]` |
| Flutter mobile | `[APK TO BE GENERATED]` | distributed build artifact |
| PostgreSQL | **DEPLOYED** — Supabase | `aws-0-ap-southeast-2.pooler.supabase.com` (database `postgres`) |
| Agentic AI service | `[CURRENT SETUP TO BE DOCUMENTED]` | no AI backend exists yet |

## Architecture

```
React web (SPA, static hosting)
Flutter app (APK, direct device install)
        │   HTTPS + Bearer JWT
        ▼
ASP.NET Core API  ──►  Supabase PostgreSQL (shared, pooled)
```

Single shared database for web + mobile (assignment requirement). All clients authenticate against the same `/api/auth/login` and carry the same JWT contract.

## Configuration required per environment

| Key | Notes |
|---|---|
| `ConnectionStrings:PetCareDb` / env `PETCARE_DB_CONNECTION` | Supabase pooled connection string — secret, never committed |
| `Jwt:Key` / env `PETCARE_JWT_KEY` | signing key — secret; generate a long random value per environment |
| `Jwt:Issuer` / `Jwt:Audience` | `PetCareApi` / `PetCareClient` (appsettings defaults) |
| `Cors:AllowedOrigins` | must include the deployed web origin (dev: `http://localhost:5173`) |
| `ASPNETCORE_ENVIRONMENT` | `Production` disables Swagger (`Program.cs` gates it to Development) |
| `VITE_API_BASE_URL` | web build-time env → deployed API URL |
| `API_BASE_URL` | mobile `--dart-define` at build time |

## Secrets handling

- **Never in Git:** connection strings, JWT keys, passwords — enforced by empty appsettings values + comments, user secrets locally, environment variables in deployment.
- Staff accounts are created by an Administrator with a server-generated one-time temporary password (`MustChangePassword = true`) — no email/SMS channel exists yet; handoff is out-of-band (documented limitation).

## Startup order

1. PostgreSQL reachable + migrations applied (`dotnet ef database update` against the target, or confirm `__EFMigrationsHistory` — already applied to Supabase)
2. API (`dotnet run` / published binary)
3. Web SPA (static files — no server-side dependency)
4. Mobile (independent; needs the API reachable)

## Database deployment

Supabase is the shared DB. Schema is owned by EF Core migrations — `InitialSchedulingBillingApproval` → `AddUsers` → `ConsolidatedDomainModel` are applied; never use `EnsureCreated()`. `dotnet ef` resolves its connection via `PETCARE_DB_CONNECTION` only (not user secrets).

## Evaluator access

`[TO BE PROVIDED]` — seeded evaluator/test accounts and demo-organization details to be documented at deployment time. No credentials are committed to the repository.
