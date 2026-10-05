# E2E — Agentic Workflow

End-to-end verification of the orchestrated consultation workflow against
the real stack: ASP.NET API → agentic-service (real Gemini calls) →
disposable Docker PostgreSQL. **Latest run: 130/130 assertions passed.**

- Script: `backend/api/tests/e2e/agentic-workflow-e2e.ps1`
- Harness README: `backend/api/tests/e2e/README.md`
- Real output: `backend/api/tests/e2e/results/latest-run.txt`

## What is asserted

- Owner/org/manager/vet registration and login (throwaway accounts)
- Workflow **auto-created** on consultation submit (`status Created`)
- PetOwner `by-consultation` returns the **reduced** status DTO
- `POST /run` as PetOwner → **403**; `POST /approve` as Veterinarian → **403**
- Manager `run` → `PendingManagerApproval`; plan ≥4 steps incl.
  `consultation_agent` + `scheduling_agent` (by `agentName`); proposal
  present; `approvedAction` null
- Reject **without** comments → **400**
- Approve → `AwaitingExamination`, `approvedAction` present, appointment
  booked (consultation `AppointmentConfirmed`; owner sees the same +
  `agentWorkflowStatus` = `AwaitingExamination`)
- Duplicate approve → **409**
- History: plan steps, ≥3 step records (consultation, scheduling,
  `human_approval`, `backend:book_appointment`), one `Approved` approval
  with `DecidedByUserId`, trajectory events include
  `plan_created|plan_fallback`, `delegated` (both agents),
  `step_validated`, `route_human_approval`, `decision_approved`,
  `final_validation_passed`, `backend_action_emitted`; `Seq` strictly
  increasing
- **Full business lifecycle on ONE workflow id**: `start` is idempotent
  (returns the same id, no duplicate row); the vet records an examination
  for the booked appointment; `GET /examinations/{id}/recommendations`
  routes through the waiting workflow (`advance`) → `diagnosis_agent`
  step Completed, status `AwaitingPrescription`; diagnosis → treatment
  record → stocked medicine → `POST /prescriptions`; `GET
  /prescriptions/treatment/{id}/inventory-plan` routes through the
  workflow → `inventory_agent` step Completed, status `Completed`
- Every `agent` step in the supervisor's plan delegated exactly once
  (asserted against the plan the LLM actually produced); no specialist
  ran outside the plan; `awaiting_event`, `examination_recorded`,
  `prescription_created` marker events persisted; `Seq` strictly
  increasing across the whole lifecycle
- `GET /agent-workflows/by-consultation/{id}` and `/history` resolve the
  same workflow id end-to-end
- Tenant isolation on resume: foreign-org manager `GET`/`POST run` → **404**
- Second consultation: reject with comments → `Rejected`, consultation
  stays `Submitted`, no appointment
- Third consultation: revision with comments → new pending proposal
- **Scheduling A** — preferred hour taken → proposal falls back to the
  nearest valid same-day window (`fallbackType=same_day_nearest_time`,
  `usedPreferredTime=false`, concrete slot ids)
- **Scheduling B** — preferred date exhausted → next available date
  (`fallbackType=next_available_date`)
- **Scheduling C** — no availability anywhere in range → `Failed` /
  `no_valid_slot`, scheduling step `NoProposal`, `reasonCode=NO_VALID_SLOT`
- **Scheduling D** — trauma symptoms → Consultation Agent emits
  `complexity=complex`, `requiredSlots=2`; Scheduling Agent proposes a
  2-slot consecutive window on the preferred date; approval books a real
  2-hour appointment (all `slotIds` `Reserved`, window spans 2h)

The script calls the ASP.NET API only — it never calls the Python service
directly (documented in the harness README). Org activation and the 9
`Available` slot rows are seeded via `docker exec -i petcare-e2e-pg psql`
against the disposable database only.

## Defects found by this E2E (all fixed, re-verified 91/91)

1. `DateTime` `Kind=Local` from Python ISO offsets rejected by Npgsql →
   `ToUtc()` normalization in `AgentWorkflowService`.
2. New workflow children emitted as UPDATEs → explicit
   `AddStep`/`AddApproval`/`AddEvent` repository methods.
3. Booking collided with pre-posted `Available` slot rows (23505) →
   slot adoption in `ConsultationWorkflowService` (adopt `Available` →
   `Reserved`; `Reserved`/`Booked` → `SchedulingConflictException`).
4. Duplicate trajectory sequence numbers across resume invocations →
   backend assigns its own strictly increasing `Seq`.
5. **Trajectory duplication on re-invocation** (found when the lifecycle
   extension first exercised `advance`): `trajectory` is an
   `operator.add` reducer field — `run`/`resume`/`advance` passed the
   checkpointed `values` back into `ainvoke`, re-appending the stored
   trajectory to itself. Fixed in `main.py` by stripping `trajectory`
   from the update payload (the checkpoint already holds it); the
   response's `trajectory_before` slice is measured on the stored count.
6. The E2E script itself: em-dashes inside double-quoted strings decoded
   as `”` under Windows PowerShell 5.1's ANSI file reading (no BOM) —
   `”` is a valid string delimiter, silently swallowing the clinical
   section into the skipped `else` branch. Em-dashes removed; file now
   carries a UTF-8 BOM. Also fixed `/treatment-records` →
   `/treatmentrecords` (ASP.NET `[controller]` token produces no hyphen).
