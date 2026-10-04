# PetCare Agentic Service

Internal FastAPI + LangGraph + Gemini microservice providing **read-only, advisory** AI agents for the PetCare system. The agents analyse data and return recommendations — they never create, modify, approve, or execute business actions. Humans and the existing PetCare workflows remain authoritative.

## Agents

| Endpoint | Agent | Reads |
|---|---|---|
| `POST /api/agents/consultation-analysis/{consultation_id}` | Consultation triage | `GET /consultations/{id}`, `GET /examinations/pet/{petId}` |
| `POST /api/agents/diagnosis-analysis/{examination_id}` | Diagnosis assist | `GET /examinations/{id}`, `GET /examinations/pet/{petId}` |
| `POST /api/agents/scheduling-planning/{request_id}` | Appointment + quotation proposal | `GET /consultations/{id}`, `GET /appointments/available-slots`, `POST /appointments/check-conflict` |
| `POST /api/agents/inventory-planning/{treatment_record_id}` | Medicine/stock/batch advisory | `GET /treatmentrecords/{id}`, `GET /prescriptions/treatment/{id}`, `GET /medicines` (paged), `GET /medicines/{id}/batches`, `GET /medicines/low-stock` |

Every agent runs the same graph: `retrieve_context → LLM analyse → Pydantic validation (+ deterministic checks) → retry ×2 → safe fallback`.

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
pytest tests -q
```

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
