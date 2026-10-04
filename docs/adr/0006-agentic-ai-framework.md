# ADR 0006 — Agentic AI Framework / Orchestration

**Status:** Decided — fully implemented (all four agents integrated, Phases 2A–2D) · **Date:** 2026

## Context

The assignment requires an agentic AI component. The AI must remain advisory: it analyses data and produces recommendations, but the existing PetCare workflows (consultation → assignment → examination → diagnosis → prescription → medicine request → inventory issue → billing → payment) stay authoritative.

## Options considered

- Semantic-kernel-style in-process orchestration inside `PetCare.Api` — rejected: adds a Python-free but heavy dependency surface inside the core API
- Separate FastAPI service with LangGraph — chosen: the agents already exist in `agentic-service/` and isolate LLM concerns from the transactional API
- Direct React → FastAPI calls — rejected: the ASP.NET API must remain the single public boundary for auth, roles and tenant scoping

## Decision

`agentic-service/` is a standalone **FastAPI + LangGraph + Gemini (`langchain-google-genai`)** microservice exposing four read-only agents:

- `POST /api/agents/consultation-analysis/{consultation_id}` — triage assessment
- `POST /api/agents/diagnosis-analysis/{examination_id}` — diagnosis suggestions
- `POST /api/agents/scheduling-planning/{request_id}` — appointment + quotation proposal
- `POST /api/agents/inventory-planning/{treatment_record_id}` — medicine/batch recommendation

Each agent follows `retrieve_context (read-only PetCare API calls) → LLM analyse → Pydantic validation → retry → deterministic safe fallback`.

**Integration boundary:** React → ASP.NET Core → `IAgenticClient`/`AgenticClient` (typed HttpClient) → Agentic Service. The service requires an `X-Internal-Key` shared secret (`AGENTIC_INTERNAL_KEY` ⇄ `AgenticService:InternalKey`) and forwards the caller's bearer token for backend reads, so role and organisation checks remain in the ASP.NET API. No browser CORS is exposed.

## Consequences

- The former mock `/ai-workflows` monitor UI was removed; real agent integration landed per-workflow (manager consultation analysis + scheduling plan, vet "AI Assist", IO "AI Plan")
- `GET /api/examinations/{id}/recommendations` remains the vet-facing contract; its keyword internals were swapped for the diagnosis agent in Phase 2A
- `GEMINI_API_KEY`, `GEMINI_MODEL`, `API_BASE_URL`, `AGENTIC_INTERNAL_KEY`, `BACKEND_TIMEOUT_SECONDS` configure the service (see `agentic-service/.env.example`)
