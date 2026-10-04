# PetCare Web — React Staff & Owner Frontend

React 19 + TypeScript + Vite SPA for the PetCare platform. It serves **all**
roles — PetOwner, Veterinarian, ClinicManager, InventoryOfficer,
Administrator — while the Flutter app (`frontend/mobile`) covers the
PetOwner-only mobile experience.

## Setup

```bash
npm install
cp .env.example .env     # then adjust values
npm run dev              # http://localhost:5173
```

### Environment variables (`frontend/web/.env`, gitignored)

| Variable | Purpose |
|---|---|
| `VITE_API_BASE_URL` | ASP.NET API base incl. `/api` (dev default `http://localhost:5019/api`) |
| `VITE_GOOGLE_MAPS_API_KEY` | Optional — enables `LocationPickerMap` (org registration) and `ClinicMap` (booking). Without it the picker shows a retryable notice and booking falls back to a clinic card list; nothing is blocked |

## Scripts

| Command | What it does |
|---|---|
| `npm run dev` | Vite dev server on `:5173` |
| `npm run build` | `tsc -b` typecheck + production bundle |
| `npm run lint` | `tsc --noEmit` typecheck only |
| `npm run test` / `npm run test:run` | Vitest suite (jsdom + Testing Library; `fetch` stubbed — no backend needed) |

## Structure

```
src/
├── components/    # shared UI primitives
├── features/      # one folder per business area: admin, approvals, auth,
│                  # billing, consultations, dashboard, inventory, manager,
│                  # pets, scheduling, shared (maps…), treatment, vet
├── layouts/       # application shells
├── lib/           # googleMaps.ts — dynamic Maps JS script loader + directionsUrl
├── pages/         # route-level pages
├── routes/        # AppRoutes.tsx — ProtectedRoute + RoleRoute gating
├── services/      # api.ts (apiRequest + ApiError, Bearer header) + feature services
├── types/         # shared web types
├── utils/         # authStorage.ts (localStorage session), errors.ts, helpers
└── tests/         # Vitest + Testing Library suites
```

## Key conventions

- **API boundary:** all calls go through `apiRequest` in `services/api.ts`,
  which attaches `Authorization: Bearer` and raises `ApiError` with the
  backend's problem-details message.
- **Routing/gating:** `/login` and `/register` are public; everything else is
  wrapped in `ProtectedRoute`, with `RoleRoute` + `features/auth/roleAccess.ts`
  per-route role lists (see `routes/AppRoutes.tsx`).
- **Advisory AI surfaces** live inside the workflow pages, not a separate
  module: "AI Consultation Analysis" + "AI Scheduling Plan" on Consultation
  Requests (CM/Admin), "AI Assist" on Examinations (Vet), "AI Plan" on
  Medicine Requests (IO). They call the backend advisory GET endpoints and
  degrade to a plain notice when the agentic service is unavailable.
- **Google Maps** is loaded dynamically by `lib/googleMaps.ts` — no npm
  dependency; the key is never committed (`VITE_GOOGLE_MAPS_API_KEY`).

## Testing

171 Vitest tests covering component render, form validation, API-integration
(mocked `fetch`), and error/empty/loading states. See
`docs/testing/react-testing.md` and `docs/testing/test-evidence-index.md`.
