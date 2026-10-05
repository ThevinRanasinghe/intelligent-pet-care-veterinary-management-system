# Deployment Guide

Intended deployment architecture for PetCare AI. **Status: not yet deployed** — everything below documents the target topology and configuration; live URLs are placeholders pending the actual deployment.

## Current deployment status

| Component | Status | URL |
|---|---|---|
| ASP.NET Core API | [TO BE DEPLOYED] | Health: `/health` (added — DB-reachability check) · Swagger: `/swagger` (config-gated) |
| React web | [TO BE DEPLOYED] | `[LIVE URL]` |
| Flutter mobile | **BUILT** — `frontend/mobile/build/app/outputs/flutter-apk/app-release.apk` (~55 MB) | distributed build artifact; device install + run verification pending |
| PostgreSQL | **DEPLOYED** — Supabase, **all EF migrations applied** (verified 2026-10-05, incl. `AddAgentWorkflows` + full live workflow smoke test) | `aws-0-ap-southeast-2.pooler.supabase.com` (database `postgres`) |
| Agentic AI service | [TO BE DEPLOYED] | internal-only FastAPI service (`agentic-service/`, `python main.py`, port `PORT`/8000) — must be reachable by the API but not publicly exposed |

## Architecture

```
React web (SPA, static hosting)
Flutter app (APK, direct device install)
        │   HTTPS + Bearer JWT
        ▼
ASP.NET Core API  ──►  Supabase PostgreSQL (shared, pooled)
        │
        │  IAgenticClient: X-Internal-Key + forwarded caller JWT
        ▼
Agentic AI service (FastAPI + LangGraph → Gemini; internal-only, no CORS)
```

Single shared database for web + mobile (assignment requirement). All clients authenticate against the same `/api/auth/login` and carry the same JWT contract.

## Configuration required per environment

| Key | Notes |
|---|---|
| `ConnectionStrings:PetCareDb` / env `PETCARE_DB_CONNECTION` | Supabase pooled connection string — secret, never committed |
| `Jwt:Key` / env `PETCARE_JWT_KEY` | signing key — secret; generate a long random value per environment |
| `Jwt:Issuer` / `Jwt:Audience` | `PetCareApi` / `PetCareClient` (appsettings defaults) |
| `Cors:AllowedOrigins` | must include the deployed web origin (dev: `http://localhost:5173`) |
| `ASPNETCORE_ENVIRONMENT` | `Production` disables Swagger unless `Swagger:Enabled=true` is set (e.g. for a demo/evaluation deployment) |
| `Swagger:Enabled` | optional opt-in to expose `/swagger` outside Development |
| `VITE_API_BASE_URL` | web build-time env → deployed API URL |
| `VITE_GOOGLE_MAPS_API_KEY` | web build-time env — enables `LocationPickerMap`/`ClinicMap`; **optional** — without it the registration picker shows a retryable "temporarily unavailable" notice and the booking map falls back to plain clinic cards; registration + booking still work (see `frontend/web/.env.example`) |
| `API_BASE_URL` | mobile `--dart-define` at build time |
| `GOOGLE_MAPS_API_KEY` | mobile — `android/local.properties` (manifest placeholder) or `--dart-define` for Flutter web; optional, list fallback without it |
| `AgenticService:BaseUrl` | API config — base URL of the deployed agentic service (e.g. `http://agentic-host:8000`) |
| `AgenticService:InternalKey` / env `PETCARE_AGENTIC_INTERNAL_KEY` | shared secret sent as `X-Internal-Key` to the agentic service — secret, never committed |
| `GEMINI_API_KEY`, `AGENTIC_INTERNAL_KEY`, `API_BASE_URL`, `GEMINI_MODEL`, `BACKEND_TIMEOUT_SECONDS`, `PORT` | agentic-service env (see `agentic-service/.env.example`) — `AGENTIC_INTERNAL_KEY` must match the API-side key |

## Secrets handling

- **Never in Git:** connection strings, JWT keys, passwords — enforced by empty appsettings values + comments, user secrets locally, environment variables in deployment.
- Staff accounts are created by the organization's **ClinicManager** (`POST /api/manager/users/veterinarians`, `/inventory-officers`) with a server-generated one-time temporary password (`MustChangePassword = true`) — no email/SMS channel exists yet; handoff is out-of-band (documented limitation).

## Startup order

1. PostgreSQL reachable + migrations applied (`dotnet ef database update` against the target, or confirm `__EFMigrationsHistory`). **Supabase is fully migrated as of 2026-10-05** — all migrations including `20261005135119_AddAgentWorkflows` are applied and the four `AgentWorkflow*` tables were verified (columns, indexes, FKs), plus a live four-agent workflow smoke test ran end-to-end against it. For any *new* environment, `dotnet ef database update` is the only required step.
2. API (`dotnet run` / published binary)
3. Agentic AI service (`python main.py`) — required for the orchestrated workflow; needs **no new configuration** beyond the existing env (`GEMINI_API_KEY`, `AGENTIC_INTERNAL_KEY`, `API_BASE_URL`, `GEMINI_MODEL`, `BACKEND_TIMEOUT_SECONDS`, `PORT`)
4. Web SPA (static files — no server-side dependency)
5. Mobile (independent; needs the API reachable)

## Database deployment

Supabase is the shared DB. Schema is owned by EF Core migrations — **all migrations are applied** as of 2026-10-05 (verified via `__EFMigrationsHistory`, information_schema, and a live smoke test); never use `EnsureCreated()`. `dotnet ef` resolves its connection via `PETCARE_DB_CONNECTION` only (not user secrets).

## Evaluator access

`[TO BE PROVIDED]` — seeded evaluator/test accounts and demo-organization details to be documented at deployment time. No credentials are committed to the repository.
