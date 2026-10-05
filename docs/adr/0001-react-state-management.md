# ADR 0001 — React State Management

**Status:** Accepted · **Date:** 2026 (implemented in `frontend/web`)

## Context

The web SPA needs auth state shared app-wide plus per-feature server data. The team wanted minimal dependencies consistent with the assignment scope.

## Options considered

- **React Context + component state** — built-in, zero dependencies
- **Redux Toolkit / Zustand** — more structure, extra dependency and boilerplate
- **TanStack Query** — server-state caching, but adds a new mental model mid-project

## Decision

React Context for auth (`features/auth/AuthContext.tsx` provides `user`, `login`, `logout`, `hasRole`); local component state + direct `services/*` calls for feature data. `src/store/` holds only test utilities — no store library is used.

## Consequences

- Zero state-management dependencies; auth propagates via context cleanly
- Feature pages re-fetch via `apiRequest` on mount — no client-side cache layer; acceptable at current scale
- If cross-page server-state caching becomes painful, introduce TanStack Query deliberately rather than growing a hand-rolled store
