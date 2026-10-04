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

## Current state (Phase 2A)

The diagnosis agent is integrated into the existing veterinarian flow:

```
GET /api/examinations/{id}/recommendations   (Vet/Manager/Admin, unchanged)
      → ExaminationService.GetRecommendationsAsync
      → IAgenticClient.AnalyzeDiagnosisAsync  (caller JWT + X-Internal-Key forwarded)
      → POST /api/agents/diagnosis-analysis/{examination_id}
      → validated DiagnosisAssessment
      → TreatmentRecommendationDto → existing React "AI Assist" modal
```

Behaviour contract:

- **Advisory only** — nothing is persisted; the vet reviews, optionally prefills the diagnosis form, and saves manually. The old hardcoded keyword engine and its fake medicine GUIDs are removed.
- **Medicine resolution** — the agent returns medicine *names* only (it is never trusted with database ids). ASP.NET resolves each name against the caller's organization-scoped catalogue by exact normalized match (name, name+strength, name+form, name+strength+form). A suggestion resolves only when exactly one catalogue record matches; unknown or ambiguous names return `medicineId: null` and are flagged "not matched to formulary" in the UI.
- **Failure handling** — timeouts, 4xx/5xx, malformed JSON, or a missing assessment return `Source = "unavailable"` with a safe placeholder message; the modal shows the message, hides "Apply", and manual diagnosis remains fully usable.
- Consultation, scheduling and inventory agents remain unwired (later phases).

The other three agents are still Phase 1 foundation-only; only diagnosis is reachable from the UI.
