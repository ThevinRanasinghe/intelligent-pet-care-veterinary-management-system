# PetCare AI

Integrated Pet Care & Veterinary Service Management System — SE3090 Software Engineering Frameworks, Assignment.

PetCare AI is a multi-tenant veterinary practice platform: pet owners request consultations and manage pets, clinic staff schedule appointments, run examinations and treatment, bill through quotations with a manager approval workflow, and manage medicine inventory with FEFO batch tracking — all against a single shared ASP.NET Core API backed by PostgreSQL on Supabase.

## User roles

| Role | Description | Account creation |
|---|---|---|
| `PetOwner` | Owns pets and consultation requests; reads own clinical history | Self-registration (`POST /api/auth/register/pet-owner`) |
| `ClinicManager` | Scheduling, quotations/billing, approval decisions, clinic oversight | Created with the organization via `POST /api/auth/register/organization` |
| `Veterinarian` | Clinical records (examinations, diagnoses, treatments, prescriptions); reads scheduling/billing | Administrator-created via `POST /api/admin/users/veterinarians` |
| `InventoryOfficer` | Medicines, suppliers, stock, reservations, dispensing | Administrator-created via `POST /api/admin/users/inventory-officers` |
| `Administrator` | Platform administration — users, organizations, staff accounts, system info | System-level; provisioned directly (no self-service endpoint) |

## Main business components

- **Auth & tenancy** — JWT login, org-scoped staff, PetOwner ownership enforcement
- **Pets & consultations** — owner-managed pets, consultation requests with status history
- **Clinical** — examinations → diagnoses → treatment records → prescriptions chain
- **Scheduling & billing** — vet slots, appointments, quotations, approval workflow
- **Medicine & inventory** — medicines, suppliers, batches (FEFO), reservations, transaction ledger
- **Administration** — org approval, user lifecycle, staff account creation, system stats
- **AI workflow UI** — monitoring interface only; agentic backend not yet implemented

## Technology stack

| Layer | Stack |
|---|---|
| Web | React 19 + TypeScript + Vite, React Router, React Context (auth) |
| Mobile | Flutter (Dart), `provider`, `flutter_secure_storage`, `http` |
| API | ASP.NET Core 8 Web API, JWT bearer auth |
| ORM/Data | Entity Framework Core 8 + Npgsql |
| Database | PostgreSQL on Supabase (shared pooled instance) |
| Tests | xUnit + Moq (backend), Vitest + React Testing Library (web), `flutter_test` + `mockito` (mobile) |

## Repository structure

```
frontend/web/      React SPA (Vite) — full staff + owner experience
frontend/mobile/   Flutter client — scheduling/billing/approval mobile app
backend/api/       ASP.NET Core solution
  src/PetCare.Api/            Controllers, middleware, DI wiring
  src/PetCare.Application/    Services, DTOs, validators, interfaces
  src/PetCare.Domain/         Entities, enums, constants
  src/PetCare.Infrastructure/ EF Core, repositories, security
  tests/                      xUnit test projects
docs/              Architecture, API, database, security, testing, setup, ADRs
infra/             CI/CD assets
```

## Quick start

### Prerequisites

- .NET 8 SDK
- Node.js + npm
- Flutter SDK (for mobile)
- Access to the team's Supabase PostgreSQL (or any PostgreSQL instance)

### Backend (`backend/api`)

```bash
cd backend/api/src/PetCare.Api
dotnet user-secrets set "ConnectionStrings:PetCareDb" "<postgres-connection-string>"
dotnet user-secrets set "Jwt:Key" "<a long random secret>"
dotnet run
```

API listens on `http://localhost:5019`; Swagger UI at `http://localhost:5019/swagger`.

### Web (`frontend/web`)

```bash
cd frontend/web
npm install
npm run dev      # http://localhost:5173
```

API base URL is read from `VITE_API_BASE_URL` (`.env` already points at `http://localhost:5019/api`).

### Mobile (`frontend/mobile`)

```bash
cd frontend/mobile
flutter pub get
flutter run --dart-define=API_BASE_URL=http://10.0.2.2:5019/api   # Android emulator
```

`10.0.2.2` is the Android emulator's host loopback. For a physical device, use the machine's LAN IP.

## Configuration keys (never commit values)

| Key | Where | Purpose |
|---|---|---|
| `ConnectionStrings:PetCareDb` | user secrets / `PETCARE_DB_CONNECTION` env | PostgreSQL connection |
| `Jwt:Key` | user secrets / `PETCARE_JWT_KEY` env | JWT signing key |
| `Jwt:Issuer` / `Jwt:Audience` / `Jwt:ExpiryMinutes` | `appsettings.json` | token metadata (defaults `PetCareApi`/`PetCareClient`/60) |
| `Cors:AllowedOrigins` | `appsettings.*.json` | frontend origins (dev: `http://localhost:5173`) |
| `VITE_API_BASE_URL` | `frontend/web/.env` | web API base URL |
| `API_BASE_URL` | `--dart-define` | mobile API base URL |
| `PETCARE_TEST_DB_CONNECTION` | env | integration-test database (optional) |

## Testing

```bash
dotnet test backend/api/PetCare.sln          # backend: Application + service tests
cd frontend/web && npm run test:run          # web: Vitest suite
cd frontend/web && npm run lint              # web: TypeScript check
cd frontend/mobile && flutter test           # mobile: unit/widget tests
```

`PetCare.Infrastructure.Tests` requires `PETCARE_TEST_DB_CONNECTION` pointing at a PostgreSQL instance; it fails fast by design without one.

## Database

EF Core migrations own the schema — applied history: `InitialSchedulingBillingApproval`, `AddUsers`, `ConsolidatedDomainModel` (all applied to Supabase). 22 domain tables; `Guid`/uuid IDs for most entities, `varchar(30)` business IDs for `Pet`/`PetOwner`/`ConsultationRequest`, int identity for `ConsultationStatusHistory`. See `docs/database/database-design.md`.

## Documentation

| Doc | Contents |
|---|---|
| `docs/setup/local-development.md` | Full environment setup guide |
| `docs/api/api-reference.md` | Complete endpoint reference |
| `docs/security/authentication-authorization.md` | Auth, roles, tenancy, ownership, secrets |
| `docs/database/database-design.md` | Schema, migrations, constraints |
| `docs/deployment/deployment-guide.md` | Deployment architecture + status |
| `docs/adr/` | Architecture decision records |
| `docs/testing/` | Test evidence per client |
| `docs/component/component-overview.md` | Layered architecture walkthrough |

## Known limitations / TODO

- **AI workflows:** UI-only — no agentic backend, orchestration, or model integration yet
- **Deployment:** API and web run locally only; hosted deployment pending (see deployment guide)
- **Staff password delivery:** admin-created staff receive a one-time temporary password shown to the admin — no email/SMS invitation channel exists yet
- **`DevelopmentSeeder` exists but is not invoked** at startup — dev accounts are provisioned manually
- **README screenshots/demo:** pending final release
