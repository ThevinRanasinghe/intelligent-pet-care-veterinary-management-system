# ADR 0008 — Cloud Deployment Platform

**Status:** Partially decided — database only · **Date:** pending for API/web

## Context

The system needs hosted deployment for evaluation. Components: ASP.NET Core API, React SPA (static), PostgreSQL, future AI service.

## Decided

- **Database: Supabase PostgreSQL** — shared, already provisioned and migrated (see ADR 0003)

## Options considered (API/web)

- `[TO BE EVALUATED]` — e.g. Azure App Service, Render, Railway, or Supabase-adjacent hosting for the API; Vercel/Netlify/static hosting for the SPA

## Decision

`[PENDING]` — API and web hosting platforms not yet selected or deployed. Live URLs are placeholders in `docs/deployment/deployment-guide.md`.

## Consequences

- `backend-ci.yml` currently gates only `main` pushes/PRs — deployment triggers will need workflow updates once a platform is chosen
