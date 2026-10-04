# PetCare AI

Integrated Pet Care & Veterinary Service Management System — SE3090 Software Engineering Frameworks, Assignment.

PetCare AI is a multi-tenant veterinary practice platform: pet owners self-register, manage pets, pick a clinic from the registered organizations (on a map with optional distance sorting), and submit consultation requests for a chosen date and fixed one-hour slot; the clinic manager assigns a veterinarian (which books the appointment); the veterinarian completes examinations, diagnoses, treatments, and prescriptions — each prescription is a medicine request the inventory officer issues or marks unavailable; the appointment's bill is generated automatically from the vet charge and issued medicines, and the inventory officer records payment. Clinics may store a map location (latitude/longitude) at registration so owners can browse them geographically and open external directions. Medicine inventory runs on FEFO batch tracking — all against a single shared ASP.NET Core API backed by PostgreSQL on Supabase. A manual quotation + manager-approval flow also remains available alongside the automated billing path.

## User roles

| Role | Description | Account creation |
|---|---|---|
| `PetOwner` | Owns pets and consultation requests; reads own appointments, clinical history, and bills | Self-registration (`POST /api/auth/register/pet-owner`) |
| `ClinicManager` | Consultation-request queue, veterinarian assignment (books appointments), staff accounts (vets/inventory officers), veterinarian work history, bill view, quotation approval decisions | Created with the organization via `POST /api/auth/register/organization` |
| `Veterinarian` | Own appointments; clinical records (examinations incl. vet charge, diagnoses, treatments, prescriptions); follow-up consultation requests | ClinicManager-created via `POST /api/manager/users/veterinarians` (own org only; auto-creates a linked `Veterinarian` profile) |
| `InventoryOfficer` | Medicines, suppliers, stock, reservations, dispensing; medicine-request queue (issue/unavailable); marks bills paid | ClinicManager-created via `POST /api/manager/users/inventory-officers` (own org only) |
| `Administrator` | Platform administration — users, organizations, staff account management, system info | System-level; provisioned directly (no self-service endpoint) |

## Main business components

- **Auth & tenancy** — JWT login, org-scoped staff, PetOwner ownership enforcement
- **Pets & consultations** — owner-managed pets, consultation requests with status history; manager assigns a vet (`POST /api/consultations/{id}/assign`), which creates the appointment; vets can file follow-up requests. Booking is date + organization + one of nine fixed one-hour slots (09:00–18:00); slot availability is exposed per organization (`GET /api/consultations/availability`, `/availability/month`) and per veterinarian for the assign flow
- **Clinic locations & maps** — organizations may register an optional map location (`Organization.Latitude/Longitude`); owners browse active clinics on a Google Map (`ClinicMap`), sort by distance (`GET /api/consultations/nearby-clinics`, `/nearest-clinic` — Haversine), and open external Google directions; organization registration offers a `LocationPickerMap` — the clinic enters its address, picks "Select Location on Map", searches/zooms, pins the exact location, and confirms; the system stores latitude/longitude automatically (the step is optional and can be completed later). Google Maps is used for map visualization, clinic location selection and directions. Bookable clinics are the active organizations registered in the PetCare system.
- **Clinical** — examinations → diagnoses → treatment records → prescriptions chain; examinations close appointments and carry the veterinarian charge
- **Scheduling & billing** — vet slots, appointments (`GET /api/appointments/mine` per-caller view), auto-generated bills on the Quotation entity (vet charge + issued medicines), payment tracking; manual quotation + approval workflow remains available
- **Medicine & inventory** — medicines, suppliers, batches (FEFO), reservations, transaction ledger; prescriptions double as medicine requests fulfilled by the inventory officer
- **Administration** — org approval, user lifecycle, staff account creation, system stats
- **Agentic AI service** — `agentic-service/` (FastAPI + LangGraph + Gemini) hosts four read-only advisory agents (consultation triage, diagnosis, scheduling/quotation, inventory), all wired into the React staff UI: manager "AI Consultation Analysis" + "AI Scheduling Plan" panels, vet "AI Assist" modal, inventory-officer "AI Plan". It is internal-only (shared `X-Internal-Key`, caller JWT forwarded)

## Technology stack

| Layer | Stack |
|---|---|
| Web | React 19 + TypeScript + Vite, React Router, React Context (auth) |
| Mobile | Flutter (Dart), `provider`, `flutter_secure_storage`, `http`, `google_maps_flutter`, `url_launcher` |
| API | ASP.NET Core 8 Web API, JWT bearer auth |
| ORM/Data | Entity Framework Core 8 + Npgsql |
| Database | PostgreSQL on Supabase (shared pooled instance) |
| Tests | xUnit + Moq (backend), Vitest + React Testing Library (web), `flutter_test` + `mockito` (mobile) |

## Repository structure

```
frontend/web/      React SPA (Vite) — full staff + owner experience
frontend/mobile/   Flutter app — Pet Owner mobile app (pets, clinic map booking, appointments, bills)
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

API base URL is read from `VITE_API_BASE_URL` (`.env` already points at `http://localhost:5019/api`). An optional Google Maps key (see `frontend/web/.env.example` and `docs/setup/local-development.md`) enables the clinic map and location picker — the script is loaded dynamically, so no npm dependency is added; without it the picker is simply skipped (location can be added later) and the booking map shows a plain clinic list, so registration and booking are never blocked.

### Mobile (`frontend/mobile`)

Flutter is used as the Pet Owner mobile application. Pet Owners can register/login, manage their pets, find active PetCare clinics on a map, submit consultation requests, view appointments and follow-up appointments, and view their bills. Clinic Manager, Veterinarian, Inventory Officer and Administrator workflows remain in the React staff application. Staff accounts that sign in on mobile see a blocking notice and a logout action.

```bash
cd frontend/mobile
flutter pub get
flutter run --dart-define=API_BASE_URL=http://10.0.2.2:5019/api   # Android emulator
```

`10.0.2.2` is the Android emulator's host loopback. For a physical device, use the machine's LAN IP.

Google Maps is used for map visualization, clinic selection and directions. Bookable clinics are the active organizations registered in the PetCare system. The key is supplied through build configuration — never committed: Android reads `GOOGLE_MAPS_API_KEY` from the gitignored `android/local.properties` (see `local.properties.example`) via a Gradle manifest placeholder; Flutter web uses `--dart-define=GOOGLE_MAPS_API_KEY=...` (`tool/flutter_web.ps1` feeds it from `local.properties`/env automatically). Without a key the clinic picker falls back to its list view, so booking still works.

## Configuration keys (never commit values)

| Key | Where | Purpose |
|---|---|---|
| `ConnectionStrings:PetCareDb` | user secrets / `PETCARE_DB_CONNECTION` env | PostgreSQL connection |
| `Jwt:Key` | user secrets / `PETCARE_JWT_KEY` env | JWT signing key |
| `Jwt:Issuer` / `Jwt:Audience` / `Jwt:ExpiryMinutes` | `appsettings.json` | token metadata (defaults `PetCareApi`/`PetCareClient`/60) |
| `Cors:AllowedOrigins` | `appsettings.*.json` | frontend origins (dev: `http://localhost:5173`) |
| `VITE_API_BASE_URL` | `frontend/web/.env` | web API base URL |
| `VITE_GOOGLE_MAPS_API_KEY` | `frontend/web/.env` | optional — enables web clinic map + location picker |
| `API_BASE_URL` | `--dart-define` | mobile API base URL |
| `GOOGLE_MAPS_API_KEY` | `android/local.properties` or env / `--dart-define` (web) | optional — enables the mobile clinic map |
| `AgenticService:BaseUrl` / `AgenticService:InternalKey` | user secrets / `PETCARE_AGENTIC_INTERNAL_KEY` env | API → agentic-service link (`X-Internal-Key`) |
| `GEMINI_API_KEY`, `AGENTIC_INTERNAL_KEY`, `GEMINI_MODEL` | `agentic-service/.env` | agentic service LLM + shared-secret config |
| `PETCARE_TEST_DB_CONNECTION` | env | integration-test database (optional) |

## Testing

```bash
dotnet test backend/api/PetCare.sln          # backend: Application + service tests
cd frontend/web && npm run test:run          # web: Vitest suite
cd frontend/web && npm run lint              # web: TypeScript check
cd frontend/mobile && flutter test           # mobile: unit/widget tests
```

`PetCare.Infrastructure.Tests` requires `PETCARE_TEST_DB_CONNECTION` pointing at a **migrated** local PostgreSQL database; it fails fast with "Set PETCARE_TEST_DB_CONNECTION…" by design without one.

## Database

EF Core migrations own the schema — migration history: `InitialSchedulingBillingApproval`, `AddUsers`, `ConsolidatedDomainModel` (all applied to Supabase), plus `20260926165049_WorkflowRedesign` (consultation→billing workflow), `20260927070805_BookingRules` (`ConsultationRequests.OrganizationId`; concurrent same-vet/slot assigns are guarded by the pre-existing unique `(VeterinarianId, Date, StartTime)` index → 409), and `20260927081008_OrganizationLocation` (`Organizations.Latitude`/`Longitude`) — all additive-only and **not yet applied to the shared Supabase database** — run `dotnet ef database update` before pointing a new API build at it. 22 domain tables; `Guid`/uuid IDs for most entities, `varchar(30)` business IDs for `Pet`/`PetOwner`/`ConsultationRequest`, int identity for `ConsultationStatusHistory`. See `docs/database/database-design.md`.

## Documentation

| Doc | Contents |
|---|---|
| `docs/README.md` | Documentation index |
| `frontend/web/README.md` | React app setup/structure/conventions |
| `frontend/mobile/README.md` | Flutter PetOwner app — full developer guide |
| `agentic-service/README.md` | Advisory AI service — agents, security, configuration |
| `docs/setup/local-development.md` | Full environment setup guide |
| `docs/api/api-reference.md` | Complete endpoint reference |
| `docs/security/authentication-authorization.md` | Auth, roles, tenancy, ownership, secrets |
| `docs/database/database-design.md` | Schema, migrations, constraints |
| `docs/deployment/deployment-guide.md` | Deployment architecture + status |
| `docs/adr/` | Architecture decision records |
| `docs/testing/` | Test evidence per client |
| `docs/component/component-overview.md` | Layered architecture walkthrough |

## Known limitations / TODO

- **Agentic AI is advisory-only:** `agentic-service/` analyses data and returns recommendations; nothing in the AI layer assigns veterinarians, approves anything, issues medicine, or touches billing. The ASP.NET API proxies agent calls through `IAgenticClient` (configured via `AgenticService:*`). All four agents (diagnosis, consultation triage, scheduling, inventory) are wired into advisory UI surfaces — the veterinarian "AI Assist" recommendations (`GET /api/examinations/{id}/recommendations`), the manager consultation-analysis and scheduling-plan panels (`GET /api/consultations/{id}/analysis`, `/scheduling-plan`), and the inventory officer's "AI Plan" (`GET /api/prescriptions/treatment/{id}/inventory-plan`). Results are review-only suggestions; AI-suggested medicine names are resolved to real catalogue ids only on a unique organization-scoped match, otherwise surfaced as advisory names without ids; AI unavailability degrades to a safe message while manual workflows remain fully usable
- **No notification platform:** "notifications" are status-driven pending-item views (manager request queue, inventory-officer medicine-request queue, dashboards) — there is no notification entity or real-time push infrastructure
- **Three migrations pending on Supabase:** `WorkflowRedesign`, `BookingRules`, and `OrganizationLocation` exist only as migration files until `dotnet ef database update` is run against the shared database
- **Maps are optional:** without a configured Google Maps key the clinic-location picker shows a retryable "temporarily unavailable" notice (the location can be added later) and the booking map falls back to a plain clinic list — registration and booking still work
- **Seeded veterinarians are not login accounts:** the 3 `HasData` `Veterinarians` rows have no `UserId`; only vets created via `POST /api/manager/users/veterinarians` (which creates the linked `Veterinarian` row) can sign in
- **`GET /api/manager/veterinarians` lists Active vets only**
- **Bill requires an appointment:** `GenerateOrRefreshBillForExaminationAsync` returns nothing for examinations without `AppointmentId`; `InvoiceNumber` is derived (`INV-` + first 8 hex chars of the quotation `Id`), not a sequential counter
- **Flutter is PetOwner-only:** pets, clinic-map booking, consultation requests, appointments and read-only bills are in the mobile app; all staff workflows (approvals, slots, inventory, assignment) remain on the React web app. The Google Map needs a local API key; without it the clinic picker is a plain list
- **Deployment:** API and web run locally only; hosted deployment pending (see deployment guide)
- **Staff password delivery:** manager-created staff receive a one-time temporary password shown to the manager — no email/SMS invitation channel exists yet
- **`DevelopmentSeeder` exists but is not invoked** at startup — dev accounts are provisioned manually
- **README screenshots/demo:** pending final release
