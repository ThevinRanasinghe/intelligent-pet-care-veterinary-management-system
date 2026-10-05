# PetCare Agentic Service

Internal FastAPI + LangGraph + Gemini microservice providing **read-only, advisory** AI agents for the PetCare system. The agents analyse data and return recommendations — they never create, modify, approve, or execute business actions. Humans and the existing PetCare workflows remain authoritative.

## Agents

| Endpoint | Agent | Reads |
|---|---|---|
| `POST /api/agents/consultation-analysis/{consultation_id}` | Consultation triage + duration estimate | `GET /consultations/{id}`, `GET /examinations/pet/{petId}` |
| `POST /api/agents/diagnosis-analysis/{examination_id}` | Diagnosis assist | `GET /examinations/{id}`, `GET /examinations/pet/{petId}` |
| `POST /api/agents/scheduling-planning/{request_id}` | Appointment + quotation proposal | `GET /consultations/{id}`, `GET /appointments/available-slots`, `POST /appointments/check-conflict` |
| `POST /api/agents/inventory-planning/{treatment_record_id}` | Medicine/stock/batch advisory | `GET /treatmentrecords/{id}`, `GET /prescriptions/treatment/{id}`, `GET /medicines` (paged), `GET /medicines/{id}/batches`, `GET /medicines/low-stock` |

Every agent runs the same graph: `retrieve_context → LLM analyse → Pydantic validation (+ deterministic checks) → retry ×2 → safe fallback`.

## Supervisor orchestration (`supervisor/`)

A fifth, non-business agent — the **Supervisor/Planner** — runs an explicit
LangGraph `StateGraph` (`supervisor/graph.py`) that chains the four
specialist agents into a governed consultation→billing workflow with a
human approval gate. Full spec: `docs/agentic/orchestration-workflow.md`
and `docs/adr/0009-supervisor-orchestration-and-approval-gate.md`.

- **Plan** — the supervisor asks the LLM for a structured `WorkflowPlan`;
  `validate_plan` enforces step types, allowed agents, ordering and the
  required `human_approval` + `backend_action(book_appointment)` shape;
  on repeated failure it falls back to the deterministic `DEFAULT_PLAN`.
- **Delegate** — `select_next` (code, not the LLM) routes plan steps to
  specialists; each specialist runs with an isolated input (id key +
  `auth_token` only) and its own tool allow-list.
- **Validate** — deterministic `validate_proposal` / `final_validation`;
  failures fail safely (`Failed` + `failureReason`) or loop back.
- **Approve** — the graph pauses at `approval_gate` via `interrupt()`;
  only the ASP.NET backend resumes it (`Command(resume)`) after an
  authenticated ClinicManager decision. The LLM never decides approvals.
- **Audit** — every node emits trajectory events; the backend persists
  plan/steps/approvals/events with its own strictly increasing sequence.

Caps: `MAX_PLAN_STEPS=8`, `MAX_DELEGATIONS=6`, `MAX_REVISIONS=2`,
`MAX_SUPERVISOR_ITERATIONS=10`. Checkpointing is `MemorySaver` keyed by
`thread_id = workflowId` (per-process); the backend sends its persisted
snapshot on run/resume/advance so the service is stateless-restartable.

### Consultation agent — bounded duration estimation

The consultation assessment carries a scheduling hint for the scheduling
agent: `complexity` (`simple`/`moderate`/`complex`), `requiredSlots`,
`estimatedDurationMinutes`, `schedulingReason`, `confidence`. The LLM only
*proposes* these — `consultation_agent/duration_mapping.py` normalizes
them deterministically before anything consumes them:

- fixed 60-minute slots; `estimatedDurationMinutes` is always
  `requiredSlots * 60` — arbitrary durations (83 min) are never emitted;
- per-complexity ceilings: `simple` → 1, `moderate` → ≤2, `complex` → ≤4
  (`SCHEDULING_MAX_SLOTS` hard cap), so serious-sounding symptoms cannot
  over-book without a complexity assessment;
- missing/unrecognized complexity → treated as ≤2 ceiling, 1-slot default;
- `confidence < 0.5` cannot escalate beyond the complexity default;
- injected instructions in owner text ("book 4 slots") land here as
  out-of-range numbers and are clamped.

Every adjustment is recorded on `schedulingReason` for the audit trail,
and the scheduling agent re-validates the count again via
`resolve_required_slots` — two deterministic layers between the model
and any booking.

### Scheduling agent — deterministic slot search

`AppointmentSlot` rows are **published availability**; `Appointment` rows are
**bookings**. An empty calendar day is *not* bookable — the agent can only
propose real `Available` slot records and never fabricates them.

Slot selection is pure code (`scheduling_agent/slot_search.py`), not the LLM.
Given the owner's preferred date/time and the consultation's
`requiredSlots` (1–4 consecutive one-hour slots), the search order is:

1. **Preferred date + exact preferred time** — a consecutive window
   starting exactly at the preferred hour.
2. **Preferred date + nearest time** — the whole opening-hours window of
   the preferred date is searched; the valid window closest to the
   preferred time wins (`fallbackType: same_day_nearest_time`).
3. **Nearest future date** — later dates are searched in order up to
   `SCHEDULING_MAX_FUTURE_DAYS` (`fallbackType: next_available_date`).

The preferred **date** always outranks the preferred **time**. Only when no
valid consecutive window exists anywhere on the preferred date does the
search advance to the next date. Nothing found → deterministic
`no_proposal` with `reasonCode: NO_VALID_SLOT` (the LLM is not even called).

Constraints enforced in code: slots must be `Available`, same veterinarian,
same date, hour-aligned, exactly consecutive, and inside the configured
opening window (`SCHEDULING_OPEN_HOUR`–`SCHEDULING_CLOSE_HOUR`, default
09–18, mirroring the backend's `BookingRules`). Diagnostics (`datesSearched`,
`candidatesConsidered`, `requiredSlots`, `durationSource`, fallback flags)
are persisted on the proposal for the manager UI.

Required duration comes from the consultation assessment
(`estimatedDurationMinutes`/`requiredSlots`); invalid or missing values
fall back to one slot, and out-of-range values produce a deterministic
`INVALID_DURATION` failure. Multi-slot proposals carry every `slotIds`
entry — the ASP.NET backend re-validates all of them and reserves the whole
window atomically in one transaction, so approval-time staleness and
concurrent grabs fail safely.

### Workflow endpoints (all internal, `X-Internal-Key` + caller JWT)

| Route | Purpose |
|---|---|
| `POST /api/workflows/{id}/run` | Run to the approval gate; returns plan, steps, this-invocation `trajectory` + `trajectoryTotal`, proposal |
| `POST /api/workflows/{id}/resume` | Inject the backend approval decision and resume the interrupted graph |
| `POST /api/workflows/{id}/advance` | Inject a lifecycle event (`examination_recorded`, `prescription_created`) and run remaining steps |
| `GET /api/workflows/{id}/state` | Checkpoint state incl. the full trajectory |

This service still **never writes business data** — the backend persists
everything and executes `book_appointment` itself after approval.

## Architecture

```
React / Flutter
    │  (JWT, roles, org scope)
    ▼
ASP.NET Core API  ── IAgenticClient (typed HttpClient) ──►  this service (:8000, internal)
                                                              │  caller JWT forwarded on data reads
                                                              ▼
                                                        PetCare API (read endpoints) + Gemini
```

- **Authentication:** every agent endpoint requires the `X-Internal-Key` header matching `AGENTIC_INTERNAL_KEY`. `/health` is the only unauthenticated route.
- **Authorization/tenancy:** enforced by the ASP.NET API — the service forwards the caller's `Authorization` bearer token on all backend reads, so role and organisation scoping are preserved.
- **No browser CORS:** the service is internal-only; no CORS middleware is registered.

## Configuration

Copy `.env.example` to `.env` (gitignored). Required:

| Variable | Purpose |
|---|---|
| `GEMINI_API_KEY` | Gemini API key — service fails clearly without it (no dummy fallback) |
| `AGENTIC_INTERNAL_KEY` | Shared secret expected in `X-Internal-Key`; agent endpoints return 503 while unset |
| `API_BASE_URL` | PetCare API base incl. `/api` (default `http://localhost:5019/api`) |
| `GEMINI_MODEL` | Model id (default `gemini-3.5-flash`) — `gemini-2.5-flash` is retired for new keys; verify against your provisioning |
| `BACKEND_TIMEOUT_SECONDS` | Backend call timeout (default `10`) |
| `PORT` | Uvicorn port for `python main.py` (default `8000`) |
| `SCHEDULING_OPEN_HOUR` | Clinic opening hour for slot search (default `9`) |
| `SCHEDULING_CLOSE_HOUR` | Clinic closing hour — last slot must end by this (default `18`) |
| `SCHEDULING_MAX_FUTURE_DAYS` | How far ahead the date fallback searches (default `30`) |
| `SCHEDULING_MAX_SLOTS` | Max consecutive slots per proposal (default `4`) |

The matching API-side config is `AgenticService:BaseUrl` + `AgenticService:InternalKey` (user-secrets or `PETCARE_AGENTIC_INTERNAL_KEY`).

## Run

```bash
pip install -r requirements.txt
python main.py            # :8000
curl localhost:8000/health
```

## Test

```bash
pip install -r requirements-dev.txt
pytest tests -q                        # unit + graph + endpoint tests (121)
python -m tests.evaluation.run_eval    # deterministic evaluation suite (32)
```

The evaluation suite is deterministic golden cases + prompt-injection
defenses with the LLM/backend mocked — see
`docs/testing/agentic-evaluation.md` and `tests/evaluation/results/latest.json`.

## Current state — all four agents integrated (Phases 2A–2D)

Every agent is reachable through an advisory GET endpoint on the ASP.NET API, which proxies to this service via `IAgenticClient` and renders the result in the React staff UI:

```
Consultation triage (Phase 2B)
GET /api/consultations/{id}/analysis            (ClinicManager/Admin)
      → ConsultationRequestService.GetAnalysisAsync
      → POST /api/agents/consultation-analysis/{consultation_id}
      → ConsultationAnalysisDto → manager "AI Consultation Analysis" panel
        on the Consultation Requests page

Scheduling plan (Phase 2C)
GET /api/consultations/{id}/scheduling-plan     (ClinicManager/Admin)
      → ConsultationRequestService.GetSchedulingPlanAsync
      → POST /api/agents/scheduling-planning/{request_id}
      → SchedulingPlanDto → manager "AI Scheduling Plan" panel
        (recommended vet/slot + quotation draft; assignment stays manual)

Diagnosis assist (Phase 2A)
GET /api/examinations/{id}/recommendations      (Vet/Manager/Admin)
      → ExaminationService.GetRecommendationsAsync
      → POST /api/agents/diagnosis-analysis/{examination_id}
      → TreatmentRecommendationDto → vet "AI Assist" modal

Inventory plan (Phase 2D)
GET /api/prescriptions/treatment/{id}/inventory-plan   (InventoryOfficer/Admin)
      → MedicineRequestService.GetInventoryPlanAsync
      → POST /api/agents/inventory-planning/{treatment_record_id}
      → InventoryPlanDto → "AI Plan" action on the Medicine Requests page
```

Behaviour contract (all agents):

- **Advisory only** — agents analyse and recommend; nothing is persisted and no workflow action (assign, dispense, bill, approve) is taken automatically. The human stays authoritative.
- **Medicine resolution** — agents return medicine *names* only (never database ids). ASP.NET resolves each name against the caller's organization-scoped catalogue by exact normalized match (name, name+strength, name+form, name+strength+form). Only a unique match resolves to a `medicineId`; unknown/ambiguous names surface as advisory text flagged "not matched to formulary".
- **Deterministic validation** — LLM output passes Pydantic schema validation plus deterministic checks (real dates, slot bounds, stock quantities); invalid output retries (×2) then falls back.
- **Non-retryable errors fast-path** — quota/rate-limit (429) and other permanently-failing LLM errors short-circuit to the safe fallback instead of burning retries.
- **Failure handling** — timeouts, 4xx/5xx, malformed JSON, or a missing result return `Source = "unavailable"` (or the endpoint's safe fallback); the UI shows a plain-language notice and the manual workflow remains fully usable.
