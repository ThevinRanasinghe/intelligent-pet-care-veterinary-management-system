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
| `PetCare.Tests` | 131/131 | Pure unit/service tests |
| `PetCare.Application.Tests` | 184/184 | Pure unit tests |
| `PetCare.Infrastructure.Tests` | 17/21 | 4 tests require `PETCARE_TEST_DB_CONNECTION` → a migrated disposable PostgreSQL DB; they fail fast by design without it |

## Database

EF Core migrations own the schema — `dotnet ef database update` against the
target with `PETCARE_DB_CONNECTION` set in the same shell. Never use
`EnsureCreated()`. See `docs/database/database-design.md` and
`docs/setup/local-development.md` for the full setup guide.
