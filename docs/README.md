# Documentation Index

Map of all project documentation. Start at the root `README.md` for the
platform overview; each entry point below links deeper.

## Getting started

| Doc | Contents |
|---|---|
| `setup/local-development.md` | Full local setup: prerequisites, secrets/user-secrets, run/test/build commands for backend, web, mobile, and the agentic service, database migrations |
| `../frontend/web/README.md` | React app entry point — env vars, scripts, structure, conventions |
| `../frontend/mobile/README.md` | Flutter PetOwner app — structure, auth, Maps config, run/build/test, troubleshooting |
| `../agentic-service/README.md` | Advisory AI service — agents, security model, configuration, integration contract |

## System design

| Doc | Contents |
|---|---|
| `component/component-overview.md` | End-to-end architecture walkthrough — layers, controllers, services, entities, security boundary |
| `architecture/component-boundary.md` | Scope of the Scheduling/Billing/Approval component and the advisory-AI boundary |
| `architecture/folder-structure.md` | Shared top-level repository layout |
| `database/database-design.md` | Schema, migrations, constraints |
| `database/scheduling-billing-approval-domain-model.md` | Domain model for scheduling/billing/approval |
| `database/medicine-inventory-domain-model.md` | Medicine/inventory domain model (FEFO, batches, reservations) |
| `business/scheduling-billing-approval-workflow.md` | Full business workflow: request → assign → examine → prescribe → fulfil → bill → pay; state machines, human-decision points |
| `agentic/orchestration-workflow.md` | Supervisor orchestration: LangGraph plan/delegate/validate/approve-gate, caps, persistence, prompt-injection defenses, endpoints |

## API & security

| Doc | Contents |
|---|---|
| `api/api-reference.md` | Complete endpoint reference generated from the controllers, incl. the advisory AI endpoints |
| `api/scheduling-billing-approval-api-contract.md` | Planned/verified contract for the scheduling-billing-approval module |
| `security/authentication-authorization.md` | JWT auth, role/permission matrix, tenancy/ownership rules, agentic-service security boundary, secrets handling |

## Testing & verification

| Doc | Contents |
|---|---|
| `testing/test-evidence-index.md` | Master log of per-step test results and commits (Steps 3–28) |
| `testing/react-testing.md` | React/Vitest suite record |
| `testing/flutter-testing.md` | Flutter test/analyze verification record |
| `testing/agentic-evaluation.md` | Deterministic supervisor evaluation — 15 golden cases + prompt-injection defenses (32/32) |
| `testing/e2e-agentic-workflow.md` | Agentic-workflow E2E harness and real 57/57 run |
| `testing/performance-evidence.md` | Measured local performance of API + agentic workflow |

## Process records

| Doc | Contents |
|---|---|
| `adr/` | Architecture decision records 0001–0009 (state management, shared DB, layered backend, tenant isolation, agentic framework + state schema, deployment platform, supervisor orchestration & approval gate) |
| `ai/AI-Usage-Log-Member4.md` | AI-assistance disclosure log (Entries 01–25) |
| `devops/ci-cd.md` | GitHub Actions backend CI workflow |
| `deployment/deployment-guide.md` | Deployment topology, per-environment configuration, secrets handling |
