# Consolidated Report — Source Outline (for the final PDF)

Source content for the assignment report. Fill bracketed placeholders;
everything else is verified content. Keep each section concise — evidence
lives in `docs/final/evidence/` and `docs/testing/`.

## 1. Introduction / Project Overview
PetCare AI — a veterinary-management platform: React web + Flutter mobile +
ASP.NET Core 8 API + PostgreSQL (Supabase) + an internal FastAPI/LangGraph
agentic service that orchestrates four specialist agents under a Supervisor.

## 2. Business Problem and Scope
Clinic workflow from consultation request through scheduling, examination,
diagnosis, prescription, and inventory — with AI assistance that *proposes*
but never *decides* (human approval gates, backend-authoritative actions).

## 3. Requirements and Roles
PetOwner · ClinicManager · Veterinarian · InventoryOfficer · Admin.
Feature list per role: `docs/business/scheduling-billing-approval-workflow.md`,
`docs/security/authentication-authorization.md`.

## 4. System Architecture
Layered: clients → API → PostgreSQL + agentic service. ADR list in §22.
`docs/architecture/component-boundary.md`.

## 5. Full-Stack Architecture
React (Vite/TS, service layer) · Flutter (dart-define config) · REST+JWT.

## 6. Agentic AI Architecture
`agentic-service/`: FastAPI + LangGraph `StateGraph` — plan → validate_plan
→ select_next (code routing) → delegate → interrupt() approval gate →
backend_action emit → await business events → continue → final validation.
`docs/agentic/orchestration-workflow.md`.

## 7. Four Specialist Agents
consultation_agent (triage) · scheduling_agent (slot proposal + quotation)
· diagnosis_agent (exam recommendations) · inventory_agent (stock plan).
Narrow scopes, per-agent tool allow-lists (`shared/tool_registry.py`),
validated structured outputs.

## 8. Supervisor/Planning Agent
LLM planner emits a structured `WorkflowPlan`; `validate_plan` enforces
allow-list/ordering/≤8 steps; retry then `DEFAULT_PLAN` fallback
(`plan_fallback` event). Dynamic planning proven by
`tests/test_dynamic_planning.py` (6 tests).

## 9. Workflow Lifecycle
Created on consultation submit → run (plan+specialists) → manager approval
→ backend booking → await examination → diagnosis_agent → await
prescription → inventory_agent → Completed. Same Workflow ID throughout
(`thread_id`). Evidence: `evidence/live-smoke-test.md`.

## 10. Human Approval
`interrupt()` pause; backend `POST /approve|/reject|/revision`
(ClinicManager only); duplicate decision → 409; LLM cannot self-approve.
Persisted in `AgentWorkflowApprovals`.

## 11. Database / ERD
`docs/database/database-design.md` + 4 `AgentWorkflow*` tables
(migration `AddAgentWorkflows`, applied to Supabase —
`evidence/supabase-migration.md`).

## 12. API Design
`docs/api/api-reference.md`; `/health`; Swagger (config-gated).

## 13. React Design
Feature-folder SPA; `AgentWorkflowPanel.tsx` (plan/steps/approvals/
trajectory); `agentWorkflowService.ts`.

## 14. Flutter Design
`frontend/mobile/`; workflow status surfaces; `API_BASE_URL` via dart-define.

## 15. Security
JWT + role authz + org tenant isolation + internal `X-Internal-Key` +
input sanitization + tool allow-lists + allow-listed backend actions.
Security audit: working tree clean (`.env` ignored; zero secret hits);
one historical doc password → rotation recommended (§ compliance matrix).

## 16. Testing
Backend 137/137 + 203/203 + 25/25 (PostgreSQL) · Agentic 127/127 ·
React 184/186 (2 pre-existing flake) · Flutter 115/115 · E2E 91/91.

## 17. Agentic Evaluation
`docs/testing/agentic-evaluation.md` — golden cases, trajectory,
dynamic-planning, tool-selection, approval-enforcement tests.

## 18. Prompt-Injection Resistance
`tests/evaluation/test_prompt_injection.py` + `shared/sanitize.py`.

## 19. Performance Testing
`docs/testing/performance-evidence.md` — API ~7-26 ms hot paths,
~464-497 req/s reads; agentic run ~9 s (LLM-bound, expected).

## 20. CI/CD
`.github/workflows/backend-ci.yml` — restore/build/test + postgres
service + EF migrations (updated this phase). Evidence: [CI run link — pending push].

## 21. Deployment
`docs/deployment/deployment-guide.md`. Supabase deployed + migrated.
API/Web/agentic deploy steps documented — cloud URLs [pending / local
demo acceptable per assignment rules — confirm with lecturer].

## 22. ADRs
`docs/adr/0001-0009` — incl. 0006 agentic framework, 0007 workflow-state
schema, 0008 cloud platform, 0009 supervisor orchestration + approval gate.

## 23. Challenges and Solutions
- Trajectory duplication on graph re-invocation (operator.add reducer) —
  fixed by stripping persisted trajectory before `ainvoke` (ADR 0009).
- PS 5.1 ANSI decoding of UTF-8 em-dash inside double-quoted strings —
  swallowed a script block; normalized to ASCII + BOM.
- Booking-rule strictness (`EndTime = Start+1h`) vs seeded `:59` slots.
- CI lacked PostgreSQL for `WebApplicationFactory` tests — added service.

## 24. Individual Contributions
[PLACEHOLDER — per member, pull from `individual-evidence-checklist.md`]

## 25. AI Usage / Declaration
`docs/ai/` usage logs [+ group declaration — PLACEHOLDER].

## 26. References
[LangGraph docs, EF Core, Supabase, etc.]

## 27. Demo / Live Links
Repo: github.com/ThevinRanasinghe/intelligent-pet-care-veterinary-management-system
(branch `Merge_2`). Live URLs: [pending deploy or local demo].
APK: `frontend/mobile/build/app/outputs/flutter-apk/app-release.apk`.
