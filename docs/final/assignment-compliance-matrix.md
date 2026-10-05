# Assignment Compliance Matrix — PetCare AI (Merge_2)

Master checklist for final submission. Status legend: **PASS** (verified with evidence), **PARTIAL** (works but incomplete evidence/coverage), **FAIL** (defect found), **NOT YET EVIDENCED** (cannot be verified without manual/external action).

Last verified: 2026-10-05.

## A. Platform & Architecture

| Requirement | Status | Evidence | File/URL | Notes | Action Required |
|---|---|---|---|---|---|
| ASP.NET Core Web API | PASS | Builds clean (Release); serves all business + workflow endpoints | `backend/api/src/PetCare.Api/` | .NET 8, controllers + service layer + EF Core | none |
| PostgreSQL | PASS | Supabase (pooled) in use; schema owned by EF migrations | `docs/deployment/deployment-guide.md` | All migrations applied & verified 2026-10-05 | none |
| React (Vite + TS) | PASS | `tsc` 0 errors; production build OK; 184/186 Vitest | `frontend/web/` | 2 failures = pre-existing Maps-mock flake (passes in isolation) | none |
| Flutter | PASS | `flutter analyze` clean; 115/115 tests; release APK built | `frontend/mobile/` | `app-release.apk` ~55 MB | manual install/run check |
| Backend-authoritative design | PASS | Python emits `book_appointment`; backend `AssignConsultationAsync` executes | `AgentWorkflowService.cs:355-414` | AI advisory-only throughout | none |
| Client → API → DB/Agentic topology | PASS | No client→Python call paths; `IAgenticClient` proxies | `docs/architecture/component-boundary.md` | Internal routes protected by `X-Internal-Key` | none |

## B. Roles & Business Components

| Requirement | Status | Evidence | File/URL | Notes | Action Required |
|---|---|---|---|---|---|
| Roles: PetOwner, ClinicManager, Veterinarian, InventoryOfficer (+Admin) | PASS | `[Authorize(Roles=...)]` on controllers; role-scoped repos | `docs/security/authentication-authorization.md` | Foreign-org access verified denied in E2E | none |
| Pet management | PASS | Pets CRUD + archive/restore; used in E2E | `PetsController.cs` | | none |
| Consultation requests | PASS | Create/submit → auto workflow creation | `ConsultationRequestsController.cs`, `ConsultationRequestService.cs` | | none |
| Manager review + vet assignment | PASS | Approval gate + `book_appointment` → appointment row | live smoke wf `ba29be54` | | none |
| Appointments/slots | PASS | `AppointmentSlots` + booking rules validators | `WorkflowValidators.cs` | 1h slots, 09:00–17:00 | none |
| Examinations | PASS | `POST /examinations` used in live smoke | `ExaminationsController.cs` | triggers `examination_recorded` | none |
| Diagnoses + treatments | PASS | `POST /diagnoses`, `/treatmentrecords` | live smoke | | none |
| Prescriptions | PASS | `POST /prescriptions` | live smoke | triggers `prescription_created` | none |
| Inventory fulfillment | PASS | Medicines + reservations + inventory agent | `inventory_agent/` | | none |
| Billing/quotation | PASS | Quotation flow exists in scheduling proposal | `QuotationsController.cs` | | none |
| Organization/tenant isolation | PASS | Org-scoped repos + workflow `IsInScopeAsync`; foreign-org denied (E2E) | `AgentWorkflowService.cs:347-353` | | none |

## C. Agentic AI Requirements

| Requirement | Status | Evidence | File/URL | Notes | Action Required |
|---|---|---|---|---|---|
| Agentic framework (LangGraph) | PASS | `StateGraph` w/ plan→validate→route→delegate→approval→action→await→continue | `agentic-service/supervisor/graph.py` | | none |
| Supervisor/Planning agent | PASS | LLM produces structured `WorkflowPlan`; plan events in trajectory | `supervisor/planner.py` | user objective reaches planner prompt (test-asserted) | none |
| Four specialist agents | PASS | consultation/scheduling/diagnosis/inventory all run in one workflow | live smoke `ba29be54`, E2E `6a175205` | | none |
| Planning & delegation | PASS | Plan-driven routing; different plans → different delegation sets | `tests/test_dynamic_planning.py` (6 tests) | | none |
| Structured outputs | PASS | Pydantic-validated plan + agent outputs | `supervisor/planner.py`, specialist `validate_response_node`s | | none |
| Deterministic validation | PASS | `validate_plan`: allow-list, ≤8 steps, ordering, prerequisites; tool allow-lists | `planner.py:60-140`, `shared/tool_registry.py` | | none |
| Human approval (code-enforced) | PASS | `interrupt()` gate; backend records decision; LLM cannot self-approve | `graph.py` + `AgentWorkflowsController` approve/reject/revision | duplicate decision → 409 | none |
| Workflow persistence | PASS | `AgentWorkflows/Steps/Approvals/Events` tables; checkpoint `thread_id=workflowId` | `AddAgentWorkflows` migration (applied to Supabase) | | none |
| Observability/audit trail | PASS | 33-event trajectory per workflow, seq numbers, `/history` endpoint | live smoke history | | none |
| Agentic evaluation | PASS | golden cases, trajectory, injection suites | `agentic-service/tests/evaluation/` | 127/127 pytest | none |
| Prompt-injection resistance | PASS | injection test suite + input sanitize + tool allow-list | `tests/evaluation/test_prompt_injection.py`, `shared/sanitize.py` | | none |
| Failure recovery / safe failure | PASS | `no_valid_slot` → `Failed` w/ reason, no partial writes; `agentic_unavailable` events | live smoke wf `b0753604`, `AgentWorkflowService` catch block | failed workflows don't auto-retry (by design) | none |

## D. Testing Requirements

| Requirement | Status | Evidence | File/URL | Notes | Action Required |
|---|---|---|---|---|---|
| Traditional backend tests | PASS | PetCare.Tests 137/137; Application.Tests 203/203 | `backend/api/tests/` | | none |
| DB integration testing | PASS | Infrastructure.Tests 25/25 w/ `PETCARE_TEST_DB_CONNECTION`; CI postgres service added | `.github/workflows/backend-ci.yml` | | verify CI run on push |
| React testing | PASS | Vitest 184/186 (2 = pre-existing Maps-mock flake) | `frontend/web/src/tests/` | tsc 0 err, build OK | none |
| Flutter testing | PASS | 115/115 tests, analyze clean | `frontend/mobile/test/` | | none |
| Full-stack E2E | PASS | `agentic-workflow-e2e.ps1` 91/91 assertions | `tests/e2e/results/latest-run.txt` | full 4-agent lifecycle | none |
| Agentic E2E vs deployed DB | PASS | Live smoke on Supabase: workflow `ba29be54` → Completed | `docs/final/evidence/live-smoke-test.md` | API+agentic local, DB deployed | none |
| Performance testing | PASS | benchmark run 2026-10-05, results documented | `docs/testing/performance-evidence.md`, `tests/performance/results/latest.json` | local-only numbers | none |

## E. Git / CI / Security

| Requirement | Status | Evidence | File/URL | Notes | Action Required |
|---|---|---|---|---|---|
| Git history / CI | PARTIAL | `backend-ci.yml` exists, now provisions postgres + migrations | `.github/workflows/backend-ci.yml` | Updated config not yet exercised by a real CI run | push/PR to trigger CI |
| Secrets not in repo | PASS | `.env` files gitignored; zero secret-pattern hits in changed/new files | `git check-ignore` verified | | none |
| Historical secret exposure | PARTIAL | local DB password committed in `AI-Usage-Log-Member4.md` (commit `b84401c`); redacted in working tree | git log | rotate that local password; history not rewritten per instructions | rotate password |
| Security config | PASS | JWT auth, role authz, tenant scoping, internal-key for agentic, no hardcoded creds | `docs/security/` | | none |

## F. Deployment & Access

| Requirement | Status | Evidence | File/URL | Notes | Action Required |
|---|---|---|---|---|---|
| API health endpoint | PASS | `/health` → `{"status":"Healthy","database":"reachable"}` | `Program.cs` | verified locally vs Supabase | none |
| Swagger | PASS | `/swagger` 200; config-gated outside Development | `Program.cs`, `Swagger:Enabled` | | none |
| PostgreSQL deployment/migrations | PASS | Supabase; all migrations applied incl. `AddAgentWorkflows`; schema+FK+indexes verified | `docs/final/evidence/supabase-migration.md` | | none |
| API cloud deployment | NOT YET EVIDENCED | Runs locally vs Supabase; no cloud host reachable | `docs/deployment/deployment-guide.md` | requires hosting account + env vars (documented) | deploy or demo locally |
| React live deployment | NOT YET EVIDENCED | prod build OK; no deployed URL | deployment guide | same | same |
| Agentic deployment/local instructions | PASS | local startup documented; internal auth + env var names documented | `agentic-service/README.md`, deployment guide | remote deploy optional | none |
| Android APK | PARTIAL | `app-release.apk` built (55 MB) | `frontend/mobile/build/app/outputs/flutter-apk/` | device install/run NOT yet verified | install APK manually |

## G. Documentation & Submission

| Requirement | Status | Evidence | File/URL | Notes | Action Required |
|---|---|---|---|---|---|
| README completeness | PASS | overview, roles, architecture, setup, agentic model | `README.md` | | none |
| Docs coverage (setup/testing/security/DB/API/deployment) | PASS | `docs/` subtrees populated | `docs/` | | none |
| ADRs | PASS | 0001–0009 incl. orchestration/approval-gate + workflow-state schema + cloud platform | `docs/adr/` | ADR 0009 updated w/ trajectory defect disclosure | none |
| AI usage log | PASS | `docs/ai/AI-Usage-Log-*.md` | `docs/ai/` | one log contained a local password — redacted in tree, still in history | rotate + final member check |
| Group AI declaration | PARTIAL | AI usage logs exist per member | `docs/ai/` | confirm a group-level declaration exists/signed | member action |
| Individual contribution evidence | PARTIAL | real git log captured in checklist | `docs/final/individual-evidence-checklist.md` | per-member confirmation needed | member action |
| Demo/viva evidence | PARTIAL | demo script + live workflow evidence prepared | `docs/final/demo-script.md`, `docs/final/evidence/` | screenshots/video = manual capture | manual capture |
| Final submission links/access | NOT YET EVIDENCED | repo link known; live URLs pending deploy | — | evaluator test accounts = manual seeding | at deployment |

## Summary

- **PASS**: 34 items · **PARTIAL**: 6 items · **FAIL**: 0 · **NOT YET EVIDENCED**: 3 (cloud API deploy, cloud React deploy, evaluator access) — all blocked on external hosting accounts, not on code.
