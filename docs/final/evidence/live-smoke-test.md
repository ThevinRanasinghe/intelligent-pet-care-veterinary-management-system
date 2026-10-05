# Live Smoke Test Evidence — Full Four-Agent Lifecycle

Date: 2026-10-05
Stack: ASP.NET Core API (localhost:5019) + agentic-service (localhost:8000) + **Supabase production DB**
Note: the API/service ran locally; the database is the real deployed Supabase instance (all migrations applied). This verifies the deployed DB + application code path end-to-end. A fully hosted API URL remains a deployment gap (see compliance matrix).

## Workflow under test

- Workflow ID: `ba29be54-aedc-4d82-84e2-0794cc7c87f5` (same ID throughout)
- Consultation: `CON-10C87890` (auto-created workflow on submit)
- Owner: `sown248742@smoke.test` (throwaway) · Manager: `scm214928@smoke.test` · Vet: `svet214928@smoke.test` · IO: `sio21753@smoke.test` — all SMOKE-TEST throwaway accounts
- Pet: `PET-0D8A5C72` · Appointment: `d7a8b65a-6d18-42b7-adeb-07004a0121c4` (2026-10-06 10:00, AI-proposed slot)
- Examination: `ea8900e5-b8be-4396-84a6-4e06d94ebbbe` · Diagnosis: `7f457787` · Treatment: `4389c084` · Prescription: `53f14cb7`

## Step-by-step results

| Step | Call | Result |
|---|---|---|
| Register pet | `POST /pets` | 201 `PET-0D8A5C72` |
| Create consultation | `POST /consultations` | 201 `CON-10C87890` |
| Submit | `POST /consultations/{id}/submit` | 200 — workflow auto-created (`Created`) |
| Lookup by consultation | `GET /agent-workflows/by-consultation/{id}` | 200 — `ba29be54` |
| Run | `POST /agent-workflows/{id}/run` | 200 — plan created, consultation_agent + scheduling_agent Completed → `PendingManagerApproval` |
| Approve | `POST /agent-workflows/{id}/approve` | 200 — `human_approval` + `backend:book_appointment` Completed → `AwaitingExamination` |
| Examination | `POST /examinations` (vet) | 201 `ea8900e5` |
| Recommendations call | `GET /examinations/{id}/recommendations` (vet) | 200 — `examination_recorded` resumed graph; diagnosis_agent Completed → `AwaitingPrescription` |
| Diagnosis | `POST /diagnoses` | 201 |
| Treatment record | `POST /treatmentrecords` | 201 |
| Prescription | `POST /prescriptions` | 201 `53f14cb7` |
| Inventory plan call | `GET /prescriptions/treatment/{trId}/inventory-plan` (IO) | 200 — `prescription_created` resumed graph; inventory_agent Completed |
| Final state | `GET /agent-workflows/{id}` | **`status=Completed`** — all four specialists ran |
| History | `GET /agent-workflows/{id}/history` | 200 — **33 events, 1 approval**, no duplicates |

## Trajectory (33 events, in order)

```
plan_created → route_agent → delegated → step_recorded → step_validated
→ route_agent → delegated → step_recorded → step_validated
→ route_human_approval → awaiting_decision → decision_approved
→ final_validation_passed → route_backend_action → backend_action_emitted
→ route_await_event → awaiting_event
→ plan_reused → route_agent → delegated → step_recorded → step_validated
→ route_await_event → awaiting_event → examination_recorded
→ plan_reused → route_agent → delegated → step_recorded → step_validated
→ route_done → workflow_completed
→ prescription_created
```

## Verified properties

- Same Workflow ID across run → approval → examination → prescription → completion.
- Human approval enforced by backend (`POST /approve`, ClinicManager role).
- Business events (`examination_recorded`, `prescription_created`) resume the SAME LangGraph checkpoint (`thread_id = workflowId`).
- Backend performed the booking (appointment row exists), not the Python service.
- `plan_reused` events show the persisted plan drives later continuations — no re-planning.

## Earlier same-day evidence

- Workflow `b0753604-…`: scheduling found no slots → `Failed / no_valid_slot` — safe-failure behavior verified; re-run replays to `safe_failure` without re-delegation (by design — failed steps are not retried).
- Workflow `d86bfed8-…`: approval succeeded; `book_appointment` failed `booking_failed:ValidationException` because seeded slots used `:59` end times (`EndTime must equal StartTime+1h`). Fixture defect, fixed; NOT a product defect.
- Local disposable-stack E2E (`agentic-workflow-e2e.ps1`): **91/91 assertions**, workflow `87330f64-…` Completed — see `backend/api/tests/e2e/results/latest-run.txt`.

## Defect found and fixed during this verification

During the Supabase smoke run, workflow `ba29be54` step rows displayed a
merge artifact: `diagnosis_agent/backend:book_appointment`. Root cause —
`AgentWorkflowService.ApplyRunResult` paired incoming graph steps with
persisted rows by raw list index; backend-inserted rows
(`Task = "backend:book_appointment"`) share the graph's step number and
shifted the pairing. Fixed by matching on `(StepNumber, Task)` and never
merging into backend-owned (`backend:`-prefixed) rows. Regression test:
`Advance_never_merges_specialist_steps_into_backend_owned_rows`
(`AgentWorkflowServiceTests`). Re-verified: local E2E 91/91 and the Docker
DB shows correctly labeled rows (separate `backend_action` /
`backend:book_appointment`, specialists on steps 5/6).
