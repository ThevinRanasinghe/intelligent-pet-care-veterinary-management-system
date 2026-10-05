# Agentic Workflow E2E (`agentic-workflow-e2e.ps1`)

End-to-end verification of the AI-supervised consultation workflow against a
**local disposable stack**. The script talks to the ASP.NET API only — it never
calls the Python agentic service directly (the backend is the only path into
`/api/workflows/*`).

## Prerequisites / startup order

1. Postgres 16 in Docker (disposable):

   ```powershell
   docker run -d --name petcare-e2e-pg -e POSTGRES_PASSWORD=petcare `
     -e POSTGRES_DB=petcare_e2e -p 55432:5432 postgres:16
   ```

2. Apply migrations to that container only (never Supabase):

   ```powershell
   cd backend/api
   $env:PETCARE_DB_CONNECTION = 'Host=localhost;Port=55432;Database=petcare_e2e;Username=postgres;Password=petcare'
   dotnet ef database update --project src/PetCare.Infrastructure --startup-project src/PetCare.Infrastructure
   ```

3. Agentic service on :8000 (`agentic-service/.venv/Scripts/python.exe main.py`
   from `agentic-service/`, with `API_BASE_URL=http://localhost:5145/api`
   exported so the in-repo `.env` value is overridden).

4. API on :5145 (env overrides beat user-secrets — important so the Supabase
   connection string in user-secrets is NOT used):

   ```powershell
   cd backend/api
   $env:ASPNETCORE_ENVIRONMENT = 'Development'               # swagger + env overrides
   $env:ConnectionStrings__PetCareDb = 'Host=localhost;Port=55432;Database=petcare_e2e;Username=postgres;Password=petcare'
   $env:PETCARE_JWT_KEY = '<any random string>'
   $env:PETCARE_AGENTIC_INTERNAL_KEY = '<value of AGENTIC_INTERNAL_KEY in agentic-service/.env>'
   $env:AgenticService__BaseUrl = 'http://localhost:8000'
   $env:ASPNETCORE_URLS = 'http://localhost:5145'
   dotnet run --project src/PetCare.Api --no-launch-profile
   ```

## Environment variables

| Var | Default | Purpose |
|-----|---------|---------|
| `E2E_API` | `http://localhost:5145/api` | API base URL |
| `E2E_PG_CONTAINER` | `petcare-e2e-pg` | Docker Postgres container used for org activation |

## Org activation + slot seeding

Organization activation uses **`docker exec -i <container> psql`** (SQL piped
via stdin — quoting stays intact on Windows). Direct DB writes are used ONLY
for two fixture operations that have no API surface:

- `UPDATE "Organizations" SET "Status"=1, "IsActive"=true` — activates the
  freshly registered org (simulates a SuperAdmin approving it).
- `INSERT INTO "AppointmentSlots" ... 'Available'` — posts 9 hourly slots
  (09:00–17:00) for the new vet on the work date. Required because the booking
  model materializes a `Reserved` slot only at assignment time;
  `/appointments/available-slots` only sees pre-posted `Available` rows, so
  without this the scheduling agent can only conclude `no_valid_slot`.

## What is asserted

Setup → owner/org/manager/vet/pet; workflow auto-created on submit (Created);
owner sees reduced status DTO; PetOwner `run` → 403; manager `run` →
PendingManagerApproval with a >=4-step plan incl. consultation_agent and
scheduling_agent, proposal present, `approvedAction` null, one pending
approval; Veterinarian `approve` → 403; reject without comments → 400;
approve → AwaitingExamination + `approvedAction` + real appointment
(consultation `AppointmentConfirmed`, owner sees the workflow status);
duplicate approve → 409; `history` returns plan, >=3 steps covering
consultation_agent/scheduling_agent/`backend:book_appointment`, one Approved
approval, trajectory events incl. plan_created|plan_fallback, delegated,
step_validated, route_human_approval, decision_approved,
final_validation_passed, backend_action_emitted with strictly increasing
`seq`; second workflow rejected (consultation stays Submitted, no
appointment); third workflow revision → new pending approval (or
Failed/no_valid_slot, logged).

Scheduling-priority scenarios (deterministic slot search):

- **A — same-day fallback:** the preferred hour's slot is `Reserved`; the
  proposal must stay on the preferred date with
  `fallbackType=same_day_nearest_time`, `usedPreferredTime=false`,
  `usedPreferredDate=true`, and concrete `slotIds`.
- **B — next-date fallback:** nothing available on the preferred date;
  the proposal picks the nearest seeded future date with
  `fallbackType=next_available_date`, `usedPreferredDate=false`.
- **C — no availability anywhere:** preferred date beyond the seeded
  range and the `SCHEDULING_MAX_FUTURE_DAYS` window → workflow `Failed`
  with `failureReason=no_valid_slot` and a `NoProposal` scheduling step
  carrying `reasonCode=NO_VALID_SLOT`. Availability is never invented.

## Output

`results/latest-run.txt` — console transcript of the most recent run.
Exit code non-zero on the first failed assertion.
