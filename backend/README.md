# PetCare API — ASP.NET Core Backend

Multi-tenant veterinary management API. Layered Clean Architecture over EF
Core + PostgreSQL, JWT bearer auth, role-based authorization, and organization
tenant isolation. It is the single public boundary — both clients (React web,
Flutter mobile) call it, and it proxies advisory AI calls to the internal
`agentic-service`.

## Layout

```
api/
├── PetCare.sln
├── src/
│   ├── PetCare.Api/             # Controllers, middleware, DI wiring, JWT auth, Swagger
│   ├── PetCare.Application/     # Use-case services, DTOs, FluentValidation, interfaces
│   ├── PetCare.Domain/          # Entities, enums, Roles constants (no dependencies)
│   └── PetCare.Infrastructure/  # EF Core DbContext, repositories, JwtTokenGenerator, migrations
└── tests/
    ├── PetCare.Tests/                 # Service/API unit tests
    ├── PetCare.Application.Tests/     # Application-layer unit tests
    └── PetCare.Infrastructure.Tests/  # PostgreSQL-backed integration tests
```

## Required configuration (never committed)

The API **refuses to start** without these two values:

```bash
cd api/src/PetCare.Api
dotnet user-secrets set "ConnectionStrings:PetCareDb" "<postgres-connection-string>"
dotnet user-secrets set "Jwt:Key" "<a long random secret>"
```

Environment-variable equivalents: `PETCARE_DB_CONNECTION` (also the **only**
source `dotnet ef` reads), `PETCARE_JWT_KEY`, `PETCARE_TEST_DB_CONNECTION`
(integration tests), `PETCARE_AGENTIC_INTERNAL_KEY`.

Optional agentic link (advisory AI panels degrade to a safe fallback without it):

```bash
dotnet user-secrets set "AgenticService:BaseUrl" "http://localhost:8000"
dotnet user-secrets set "AgenticService:InternalKey" "<same as AGENTIC_INTERNAL_KEY>"
```

## Run

```bash
dotnet build api/PetCare.sln
dotnet run --project api/src/PetCare.Api
```

- API: `http://localhost:5019` · Swagger: `http://localhost:5019/swagger`
  (Development only)

## Tests

```bash
dotnet test api/PetCare.sln
```

| Project | Latest verified | Notes |
|---|---|---|
| `PetCare.Tests` | 137/137 | Pure unit/service tests (incl. agent-workflow persistence) |
| `PetCare.Application.Tests` | 201/201 | Pure unit tests (incl. `AgentWorkflowService`, slot adoption) |
| `PetCare.Infrastructure.Tests` | 25/25 | Requires `PETCARE_TEST_DB_CONNECTION` → a migrated disposable PostgreSQL DB; fails fast by design without it |

## Orchestrated AI workflow

`Controllers/AgentWorkflowsController.cs` (`api/agent-workflows`) +
`Services/AgentWorkflowService.cs` + `Repositories/AgentWorkflowRepository.cs`
+ `Configurations/AgentWorkflowConfiguration.cs` drive the LangGraph
supervisor in `agentic-service/` via `IAgenticClient`
(`RunWorkflowAsync`/`ResumeWorkflowAsync`/`AdvanceWorkflowAsync`).
Workflows auto-create on consultation submit; a ClinicManager runs them,
reviews the proposal, and approves/rejects/revises — only then does the
backend book the appointment (`AssignConsultationAsync`). Full spec:
`docs/agentic/orchestration-workflow.md`. E2E harness:
`api/tests/e2e/agentic-workflow-e2e.ps1` (57 assertions).

## Database

EF Core migrations own the schema — `dotnet ef database update` against the
target with `PETCARE_DB_CONNECTION` set in the same shell. Never use
`EnsureCreated()`. See `docs/database/database-design.md` and
`docs/setup/local-development.md` for the full setup guide.
