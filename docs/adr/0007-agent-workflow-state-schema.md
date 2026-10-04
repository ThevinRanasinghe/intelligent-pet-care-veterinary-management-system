# ADR 0007 — Database Schema for Agent Workflow State

**Status:** Decided — deferred, no schema change in Phase 1 · **Date:** 2026

## Context

The agentic AI component now exists (`agentic-service/`, see ADR 0006). Its assessments are advisory — a vet/manager/IO reads the suggestion and acts through the normal workflow. The question was whether agent runs/proposals need their own persistence.

## Options considered

- Reuse existing `Approvals`/`ApprovalHistories` for agent proposals
- Dedicated workflow-state tables (AgentRun, AgentStep, …)
- No persistence — assessments are transient responses

## Decision

**No AI schema is introduced in Phase 1.** Agent output is transient and advisory; the authoritative record remains whatever the human user creates through the existing workflow (diagnosis, prescription, quotation, approval).

If assessment auditing becomes a requirement later, the preferred path is reusing `Approvals`/`ApprovalHistories` for "agent proposal pending human decision" rather than a parallel schema.

## Consequences

- Zero migration churn; the PetCare schema is untouched by AI integration
- Agent responses are not auditable historically until/unless persistence is added — a documented, accepted trade-off
