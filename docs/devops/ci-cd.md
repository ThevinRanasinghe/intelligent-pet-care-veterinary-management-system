# CI/CD

This document describes the actual Continuous Integration configuration for the Scheduling, Billing & Approval Management component. All workflow details below are verified against `.github/workflows/backend-ci.yml`.

---

## 1. Purpose of CI

The CI workflow ensures that every change pushed to or pulled against the `main` branch is automatically verified by restoring dependencies, building the backend solution, and running the full backend test suite. If the build or any test fails, the workflow fails, preventing broken code from landing on `main` without manual intervention.

---

## 2. Workflow trigger

The workflow is triggered by:

```yaml
on:
  push:
    branches: [main]
  pull_request:
    branches: [main]
```

| Event | Target branch | Action |
|---|---|---|
| `push` | `main` | Runs the backend CI job |
| `pull_request` | `main` | Runs the backend CI job |

The workflow does **not** trigger on pushes to feature branches such as `Scheduling-Billing-Approval-Management`.

---

## 3. Environment

| Aspect | Value |
|---|---|
| Runner | `ubuntu-latest` |
| .NET SDK | `8.0.x` (via `actions/setup-dotnet@v4`) |
| Solution | `backend/api/PetCare.sln` |
| Target framework | `net8.0` (all projects) |

The .NET version is specified as a floating `8.0.x` patch range, matching the `net8.0` target framework in all six backend `.csproj` files.

---

## 4. Pipeline steps

The workflow file `.github/workflows/backend-ci.yml` defines a single job `backend` with five named steps:

| Step name | Action / Command |
|---|---|
| Checkout repository | `uses: actions/checkout@v4` |
| Setup .NET SDK | `uses: actions/setup-dotnet@v4` with `dotnet-version: '8.0.x'` |
| Restore dependencies | `dotnet restore backend/api/PetCare.sln` |
| Build solution | `dotnet build backend/api/PetCare.sln --no-restore --configuration Release` |
| Run tests | `dotnet test backend/api/PetCare.sln --no-build --configuration Release --logger "console;verbosity=normal"` |

All paths are relative to the repository root, which is the default working directory in GitHub Actions. No `working-directory` override is needed.

---

## 5. Backend test scope

The CI workflow runs the existing xUnit test projects in the backend solution:

| Test project | Tests | Scope |
|---|---|---|
| `PetCare.Application.Tests` | 67 | `SchedulingService`, `BillingService`, `ApprovalService`, `AuthService`, and all FluentValidation validators |
| `PetCare.Infrastructure.Tests` | 9 | `JwtTokenGenerator`, `PasswordHasher` |
| **Total** | **76** | Pure unit tests using xUnit + Moq |

These tests are pure unit tests — they do not reference `PetCareDbContext`, `Npgsql`, or `WebApplicationFactory`. No PostgreSQL service container is required in the CI workflow.

---

## 6. Local verification

The following local verification results were produced on 2026-09-18 by running the actual commands in the local development environment. These are **local verification results**, not GitHub-hosted CI results.

| Verification | Command | Run from | Result |
|---|---|---|---|
| Backend tests | `dotnet test backend/api/PetCare.sln --configuration Release` | Repository root | **76 passed, 0 failed** |
| React tests | `npm test -- --run` | `frontend/web` | **59 passed, 0 failed** (12 test files) |
| React build | `npm run build` | `frontend/web` | **Successful** |
| Flutter analyze | `flutter analyze` | `frontend/mobile` | **No issues found** |
| Flutter tests | `flutter test` | `frontend/mobile` | **49 passed, 0 failed** |
| Flutter APK build | `flutter build apk --debug` | `frontend/mobile` | **Build successful** |

### Important

The GitHub Actions workflow (`.github/workflows/backend-ci.yml`) currently runs **backend CI only**. React and Flutter are **not** part of the GitHub Actions workflow. The React and Flutter results above are local verification results, not CI pipeline results.

---

## 7. Branch strategy

The current feature branch is:

```
Scheduling-Billing-Approval-Management
```

This branch contains all Scheduling, Billing & Approval Management work. It has **not** been merged into `main` or any merge branch. The branch was pushed successfully to `origin`.

Relevant commits on this branch:

| Commit | Message |
|---|---|
| `b70b82b` | `ci: add backend GitHub Actions workflow` |
| `f6ccdbe` | `feat: complete flutter scheduling billing approval workflow` |
| `caff493` | `fix: improve JWT key configuration fallback` |

---

## 8. Current CI limitation

The GitHub Actions workflow is configured for pushes and pull requests targeting `main`. The feature branch `Scheduling-Billing-Approval-Management` was pushed successfully, but a **GitHub-hosted workflow run has not yet occurred** for this branch under that trigger configuration.

To trigger the workflow on GitHub:
1. Open a pull request from `Scheduling-Billing-Approval-Management` into `main`, or
2. Merge the branch into `main`.

Until one of those happens, no "GitHub Actions passed" claim can be made. The workflow's correctness has been verified by source inspection and by local execution of the same `dotnet restore` / `dotnet build` / `dotnet test` commands.

---

## 9. Future CI/CD improvements

The following are **future work**, not implemented:

| Improvement | Status | Description |
|---|---|---|
| Frontend CI (React) | **Future** | A separate workflow for `npm test`, `npm run build`, and lint on `frontend/web` |
| Flutter CI | **Future** | A separate workflow for `flutter analyze`, `flutter test`, and `flutter build apk` on `frontend/mobile` |
| Integration tests | **Future** | End-to-end tests exercising the API against a real PostgreSQL service container |
| Deployment workflow | **Future** | A workflow to build, publish, and deploy the API and clients to a hosting environment |
| Code coverage reporting | **Future** | Publishing coverlet coverage reports as a CI artifact |

None of the above are part of the current `.github/workflows/backend-ci.yml`.

---

## 10. Security

- No secrets, API keys, passwords, or connection strings appear in the workflow file.
- The workflow does not configure any secrets, environment variables, or database services.
- The backend tests are pure unit tests and do not require any external services or credentials.
