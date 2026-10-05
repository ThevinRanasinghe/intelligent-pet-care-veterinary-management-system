# ADR 0009 — Supervisor Orchestration and Human Approval Gate

**Status:** Decided — implemented (orchestration phase) · **Date:** 2026

## Context

The assignment requires the agentic AI to **plan → delegate → tools →
validate → human approval → audit**. After ADR 0006 the four agents
(consultation triage, diagnosis, scheduling plan, inventory plan) existed as
independent advisory endpoints — nothing chained them into a governed
workflow, and nothing was persisted. The existing `Approval` entity is
one-to-one with `Quotation`, so it cannot represent consultation-stage AI
proposals.

## Decision

1. **A fifth, non-business agent — the Supervisor/Planner — runs an explicit
   LangGraph `StateGraph`** (`agentic-service/supervisor/graph.py`):
   `supervisor_plan → select_next → run_specialist → record_step →
   validate_step → … → approval_gate → backend_action_marker →
   finalize | safe_failure | await_event`. Routing and safety are
   **code-level** (`select_next`, `validate_plan`, `validate_proposal`,
   `final_validation`) with hard caps (`MAX_PLAN_STEPS=8`,
   `MAX_DELEGATIONS=6`, `MAX_REVISIONS=2`, `MAX_SUPERVISOR_ITERATIONS=10`),
   not prompt-only control.
2. **Pause/resume uses LangGraph `interrupt()` / `Command(resume)` with
   `MemorySaver`**, keyed by `thread_id = workflowId` — in-process
   checkpointing only.
3. **ASP.NET is the system of record and the only writer of business
   data.** `AgentWorkflows`/`AgentWorkflowSteps`/`AgentWorkflowApprovals`/
   `AgentWorkflowEvents` (migration `AddAgentWorkflows`) persist plan,
   steps, proposals, decisions, and a backend-sequenced trajectory.
   Approval decisions are made by an authenticated ClinicManager through
   `POST /api/agent-workflows/{id}/approve|reject|revision` and injected
   into the graph — **the LLM never sees or decides approvals**. The
   approved `book_appointment` action is executed by
   `ConsultationWorkflowService.AssignConsultationAsync`, the same
   authoritative path as manual assignment.
4. **`AgentWorkflowApproval` is a separate entity** rather than reusing
   quotation `Approval` (1:1 with `Quotation`, different lifecycle:
   pending → decided, comments, proposals).
5. **Snapshot rehydration** keeps the Python service
   stateless-restartable: when the `MemorySaver` checkpoint is gone, the
   backend supplies the persisted workflow snapshot and the graph
   continues.
6. **Specialist context isolation + tool allow-list:** each specialist
   runs with only its id key + `auth_token`; all tools are registered per
   agent in `shared/tool_registry.py` (`ToolNotAllowedError` on
   cross-agent calls, Pydantic input validation, per-call tracing).

## Alternatives considered

- **Orchestration in C#** — rejected: reintroduces the heavy
  agent-orchestration dependency surface into the transactional API that
  ADR 0006 deliberately moved out.
- **One mega-agent** — rejected: no delegation evidence, no per-agent
  tool isolation, and a single prompt would have to carry authorization-
  relevant decisions we need deterministic code to control.
- **Persisting LangGraph checkpoints in Postgres from Python** —
  rejected: the Python service would need database credentials and direct
  writes, breaking the API-boundary rule (Python never writes business
  data). Backend snapshot rehydration achieves restart tolerance instead.
- **Reusing quotation `Approval`** — rejected: 1:1 `Quotation` coupling
  and a different lifecycle; consultation-stage proposals may exist before
  any quotation row.

## Consequences

**Positive**

- Full auditability: plan, per-agent steps (incl. tool calls), approvals,
  and a strictly increasing event trajectory are persisted and queryable
  via `GET /api/agent-workflows/{id}/history`.
- Safety: approvals are a backend-only decision; deterministic validation
  and allow-list routing bound every LLM output; the AI remains advisory.
- Demonstrability: a real pause/resume workflow with a visible human gate
  (React `AgentWorkflowPanel`) covering the required agentic chain.
- Viva-explainable architecture: an explicit named graph with named
  nodes/edges rather than emergent prompt chaining.

**Negative**

- Two state carriers exist (backend tables + `MemorySaver`); they are
  reconciled by snapshot rehydration but must be kept consistent.
- `MemorySaver` is per-process — horizontal scale-out of the Python
  service would need sticky sessions or a shared checkpointer.
- Revision loops re-spend LLM calls (bounded by `MAX_REVISIONS=2`).
- Four extra tables and a required migration (`AddAgentWorkflows`).

**Corrected after initial review:** `trajectory` is an `operator.add`
reducer field — re-submitting checkpointed `values` into `ainvoke` (the
re-entrant `run`, non-interrupt `resume`, and `advance` paths) appended
the stored trajectory to itself, which the backend then persisted again.
`main.py` now strips `trajectory` from the update payload before
re-invoking; the `trajectory_before` response slice is measured on the
stored count. Caught by the extended four-agent E2E and
`test_dynamic_planning.py`.
