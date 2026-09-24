# ADR 0007 — Database Schema for Agent Workflow State

**Status:** TODO — decision not yet made · **Date:** pending

## Context

If/when the agentic AI component exists, its workflow state (runs, steps, proposed actions pending approval) needs persistence — the UI already renders an approval-monitoring view fed today by `Approvals`/`ApprovalHistories`.

## Options considered

- Reuse existing `Approvals`/`ApprovalHistories` for agent proposals
- Dedicated workflow-state tables
- `[TO BE EVALUATED]` alongside ADR 0006

## Decision

`[PENDING]` — depends on the orchestration choice in ADR 0006. The approval workflow entities are already the integration point the UI expects.

## Consequences

- None yet — no schema work committed for agent state
