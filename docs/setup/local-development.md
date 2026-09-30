# Local Development Setup

Verified setup guide for the integrated PetCare AI system. Every command below was run against the current project on `Merge_2`.

## Prerequisites

| Tool | Version used | Notes |
|---|---|---|
| .NET SDK | 8.0.x (`8.0.423` verified) | Backend + EF Core tooling |
| Node.js + npm | — | React web app |
| Flutter SDK | `3.47.2` stable / Dart `3.13.2` | Mobile client |
| PostgreSQL | Supabase pooled instance (or any PG ≥ 13 for `gen_random_uuid()`) | Shared team database |

Optional: `dotnet tool install --global dotnet-ef` — needed to apply migrations (`dotnet ef database update`); the shared Supabase schema has the first three migrations but **`WorkflowRedesign` still needs to be applied** (see Database below).

## Backend (`backend/api`)

### 1. Secrets

The API **refuses to start** without two values — they are never committed:

```bash
cd backend/api/src/PetCare.Api

dotnet user-secrets set "ConnectionStrings:PetCareDb" "Host=<host>;Port=5432;Database=<db>;Username=<user>;Password=<password>;SSL Mode=Require"
dotnet user-secrets set "Jwt:Key" "<a long random secret>"
```

Equivalents via environment variables (take the same precedence):

- `PETCARE_DB_CONNECTION` — PostgreSQL connection string (also the **only** source `dotnet ef`/the design-time factory reads)
- `PETCARE_JWT_KEY` — JWT signing key
- `PETCARE_TEST_DB_CONNECTION` — connection string for `PetCare.Infrastructure.Tests`; must point at a **migrated** disposable local PostgreSQL database (apply migrations to it first). Without it those tests fail fast by design.

Resolution order (from `ServiceCollectionExtensions.cs`): `ConnectionStrings:PetCareDb` / `Jwt:Key` config value → env-var fallback → startup error with instructions.

> **EF Core tooling caveat:** `dotnet ef` commands construct the context through `PetCareDbContextFactory`, which reads **only** `PETCARE_DB_CONNECTION` (not user secrets). Set that env var in the same shell before running `dotnet ef` commands.

### 2. Run

```bash
dotnet build backend/api/PetCare.sln
dotnet run --project backend/api/src/PetCare.Api
```

- API: `http://localhost:5019` (launch profile `http`, Development)
- Swagger UI: `http://localhost:5019/swagger`
- `Development` env enables Swagger + the `Cors:AllowedOrigins` entry `http://localhost:5173`

### 3. Tests

```bash
dotnet test backend/api/PetCare.sln
```

- `PetCare.Application.Tests` — 181/181
- `PetCare.Tests` — 56/56
- `PetCare.Infrastructure.Tests` — 19/19 — **require `PETCARE_TEST_DB_CONNECTION`** pointing at a disposable, **migrated** local PostgreSQL DB (e.g. run `dotnet ef database update` against a scratch database first); they fail fast with "Set PETCARE_TEST_DB_CONNECTION…" without it (by design) — this is an environment requirement, not a code failure.

## Web frontend (`frontend/web`)

```bash
cd frontend/web
npm install
npm run dev        # Vite dev server → http://localhost:5173
```

- **API base URL:** `VITE_API_BASE_URL` in `frontend/web/.env` — currently `http://localhost:5019/api` (code default: `http://localhost:5080/api`)
- **Google Maps key (optional):** `VITE_GOOGLE_MAPS_API_KEY` in the same `.env` — enables the `LocationPickerMap` (org registration) and `ClinicMap` (owner booking). With no key (or a script-load failure) the registration picker shows a retryable "temporarily unavailable" notice (location can be added later) and the booking map falls back to plain clinic cards — registration and booking still work. Copy `.env.example` for both variable names.
  - **Google Cloud requirements:** the key's project must have **billing enabled** and the **Maps JavaScript API** + **Places API** enabled — otherwise the browser console shows `BillingNotEnabledMapError`/`ApiNotActivatedMapError` and Google renders an error overlay on the map. Key restrictions must allow the dev origin (`http://localhost:5173/*`). These are Google Cloud configuration issues, not app bugs — the app correctly passes the key to the loader (`src/lib/googleMaps.ts`, `libraries=places`, `loading=async`).
- `npm run build` — typecheck + production bundle
- `npm run lint` — `tsc --noEmit` typecheck
- `npm run test` / `npm run test:run` — Vitest (124 tests, jsdom + Testing Library; `fetch` stubbed — no backend needed)

## Mobile (`frontend/mobile`)

```bash
cd frontend/mobile
flutter pub get
flutter run --dart-define=API_BASE_URL=http://10.0.2.2:5019/api
```

- **API base URL:** `API_BASE_URL` via `--dart-define` (compile-time), default `http://10.0.2.2:5080/api` in `api_config.dart` — note the default port differs from the API's actual `5019`; always pass the define or adjust the constant.
- **Google Maps key (optional):** `android/app/src/main/AndroidManifest.xml` ships the placeholder `android:value="YOUR_GOOGLE_MAPS_API_KEY"` for `com.google.android.geo.API_KEY` — replace it locally with a real Maps SDK for Android key and **never commit the key**. Without a valid key the clinic-picker map renders blank/unavailable, but the scrollable clinic list always works, so booking is never blocked. Google Maps is used for map visualization, clinic selection and directions. Bookable clinics are the active organizations registered in the PetCare system.
- `10.0.2.2` = Android emulator → host loopback. Physical device → use the machine's LAN IP (e.g. `http://192.168.x.x:5019/api`), and ensure the API binds beyond localhost if needed.
- The app is **PetOwner-only**: staff-role sign-ins are routed to a blocking screen telling them to use the web application.
- `flutter test` — unit/widget suite (see `docs/testing/flutter-testing.md` for the recorded environment and results)
- `flutter analyze` — static analysis

## Database

The team database lives on **Supabase PostgreSQL** (pooled connection). The first three EF migrations (`InitialSchedulingBillingApproval`, `AddUsers`, `ConsolidatedDomainModel`) are applied there — but the three latest migrations — **`20260926165049_WorkflowRedesign`, `20260927070805_BookingRules`, and `20260927081008_OrganizationLocation` — are not yet applied to Supabase**. All are additive-only (new columns/indexes/FKs + one CHECK constraint), but the new API build will fail at query time against a schema that lacks them — apply them before running against the shared database:

```bash
$env:PETCARE_DB_CONNECTION="<supabase-connection-string>"   # same shell!
dotnet ef database update --project backend/api/src/PetCare.Infrastructure --startup-project backend/api/src/PetCare.Api
```

For a fresh/local database run the same command against that database — all six migrations apply in order:

```bash
$env:PETCARE_DB_CONNECTION="<target-connection-string>"   # same shell!
dotnet ef database update --project backend/api/src/PetCare.Infrastructure --startup-project backend/api/src/PetCare.Api
```

Never run `EnsureCreated()` — migrations are the schema of record.

## First-run accounts

There is no automatic seeding (`DevelopmentSeeder` exists but `Program.cs` does not call it). To get a usable system on a fresh database:

1. `POST /api/auth/register/organization` — creates a Pending org + its ClinicManager
2. `POST /api/auth/register/pet-owner` — creates a PetOwner account
3. An Administrator approves the org via `PATCH /api/admin/organizations/{id}/status` — the **first** Administrator has no API creation path and must be inserted directly (PBKDF2 `{iterations}.{b64salt}.{b64hash}` format — see `PasswordHasher.cs`) or via a seeding step
4. Staff (Veterinarian/InventoryOfficer) — the ClinicManager creates them via `/api/manager/users/*`; they land in the manager's own organization automatically. Vet creation also creates the linked `Veterinarian` profile (`UserId` bound), which is what lets them log in and see "my" appointments — the 3 seeded `HasData` veterinarian rows have no `UserId` and cannot be logged into.
