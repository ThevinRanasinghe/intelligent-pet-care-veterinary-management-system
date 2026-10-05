# ADR 0008 — Cloud Deployment Platform

**Status:** Decided — deployed 2026-10-05 · **Date:** 2026-10-05

## Context

The system needs hosted deployment for evaluation. Components: ASP.NET Core API, React SPA (static), PostgreSQL, and the FastAPI/LangGraph agentic AI service.

## Options considered

- Azure App Service / Azure Static Web Apps — managed PaaS, heavier setup cost for a student project
- Railway / Fly.io — viable, but second platform to learn
- Supabase-adjacent hosting — Supabase does not host arbitrary containers
- **Render** — single platform for Docker web services *and* static sites, free tier, GitHub auto-deploy, per-service env vars, `render.yaml` blueprint support

## Decision

- **API: Render web service** (`petcare-api`) — Docker build from `backend/api/Dockerfile`, branch `main`, health check `/health`
- **Agentic service: Render web service** (`petcare-agentic`) — Docker build from `agentic-service/Dockerfile`, branch `main`; internal-only, callers must present `X-Internal-Key` (401 otherwise)
- **React SPA: Render static site** (`petcare-web`) — `rootDir: frontend/web`, `npm ci && npm run build` → `dist/`; SPA rewrite `/* → /index.html`
- **Database: Supabase PostgreSQL** — shared, already provisioned and migrated (ADR 0003)

All three Render services auto-deploy on pushes to `main` (`autoDeployTrigger: commit`).

Rationale: one platform covers containers + static hosting, the free tier is sufficient for evaluation, Docker builds remove runtime-environment ambiguity, and keeping everything on one GitHub-connected account makes the deployment reproducible and reviewable.

## Consequences

- `AgenticService__BaseUrl` on the API points at `https://petcare-agentic.onrender.com`; `API_BASE_URL` on the agentic service points back at `https://petcare-api-9hsw.onrender.com/api` — server-to-server, no CORS involved
- API `Cors__AllowedOrigins__0` is the deployed React origin only
- Free-tier web services sleep after ~15 min idle (cold starts ~30–60 s); warm `/health` endpoints before demos. Static sites do not sleep
- `backend-ci.yml` gates `main` pushes/PRs; deployment follows the same branch automatically — no separate CD workflow needed
