# Local Development Setup

Verified setup guide for the integrated PetCare AI system. Every command below was run against the current project on `Merge_2`.

## Prerequisites

| Tool | Version used | Notes |
|---|---|---|
| .NET SDK | 8.0.x (`8.0.423` verified) | Backend + EF Core tooling |
| Node.js + npm | — | React web app |
| Flutter SDK | `3.47.2` stable / Dart `3.13.2` | Mobile client |
| PostgreSQL | Supabase pooled instance (or any PG ≥ 13 for `gen_random_uuid()`) | Shared team database |

Optional: `dotnet tool install --global dotnet-ef` — only needed to inspect/run migrations (`dotnet ef database update`); the shared Supabase schema is already migrated.

## Backend (`backend/api`)

### 1. Secrets

The API **refuses to start** without two values — they are never committed:

```bash
cd backend/api/src/PetCare.Api

dotnet user-secrets set "ConnectionStrings:PetCareDb" "Host=<host>;Port=5432;Database=<db>;Username=<user>;Password=<password>;SSL Mode=Require"
dotnet user-secrets set "Jwt:Key" "<a long random secret>"
```

Equivalents via environment variables (take the same precedence):

- `PETCARE_DB_CONNECTION` — PostgreSQL connection string
- `PETCARE_JWT_KEY` — JWT signing key

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

- `PetCare.Application.Tests` — 107/107
- `PetCare.Tests` — 35/35
- `PetCare.Infrastructure.Tests` — **require `PETCARE_TEST_DB_CONNECTION`** pointing at a disposable PostgreSQL DB; they fail fast without it (by design).

## Web frontend (`frontend/web`)

```bash
cd frontend/web
npm install
npm run dev        # Vite dev server → http://localhost:5173
```

- **API base URL:** `VITE_API_BASE_URL` in `frontend/web/.env` — currently `http://localhost:5019/api` (code default: `http://localhost:5080/api`)
- `npm run build` — typecheck + production bundle
- `npm run lint` — `tsc --noEmit` typecheck
- `npm run test` / `npm run test:run` — Vitest (80 tests, jsdom + Testing Library; `fetch` stubbed — no backend needed)

## Mobile (`frontend/mobile`)

```bash
cd frontend/mobile
flutter pub get
flutter run --dart-define=API_BASE_URL=http://10.0.2.2:5019/api
```

- **API base URL:** `API_BASE_URL` via `--dart-define` (compile-time), default `http://10.0.2.2:5080/api` in `api_config.dart` — note the default port differs from the API's actual `5019`; always pass the define or adjust the constant.
- `10.0.2.2` = Android emulator → host loopback. Physical device → use the machine's LAN IP (e.g. `http://192.168.x.x:5019/api`), and ensure the API binds beyond localhost if needed.
- `flutter test` — unit/widget suite (see `docs/testing/flutter-testing.md` for the recorded environment and results)
- `flutter analyze` — static analysis

## Database

The team database lives on **Supabase PostgreSQL** (pooled connection). All three EF migrations are already applied — a fresh clone needs **no** `dotnet ef database update` unless you're targeting a new/empty database:

```bash
$env:PETCARE_DB_CONNECTION="<target-connection-string>"   # same shell!
dotnet ef database update --project backend/api/src/PetCare.Infrastructure --startup-project backend/api/src/PetCare.Api
```

Never run `EnsureCreated()` — migrations are the schema of record.

## First-run accounts

There is no automatic seeding (`DevelopmentSeeder` exists but `Program.cs` does not call it). To get a usable system on a fresh database:

1. `POST /api/auth/register/organization` — creates a Pending org + its ClinicManager
2. `POST /api/auth/register/pet-owner` — creates a PetOwner account
3. Staff (Veterinarian/InventoryOfficer) — an Administrator creates them via `/api/admin/users/*` once an Active org exists. The **first** Administrator has no API creation path and must be inserted directly (PBKDF2 `{iterations}.{b64salt}.{b64hash}` format — see `PasswordHasher.cs`) or via a seeding step.
