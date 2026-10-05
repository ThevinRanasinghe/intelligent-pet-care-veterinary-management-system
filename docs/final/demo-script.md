# Demo Script — 8–10 minutes (lecturer / viva)

Pre-reqs: API + agentic-service running (local or deployed), Supabase DB
(all migrations applied), React dev server or build, seeded demo accounts
(owner/manager/vet/IO) + a vet with `Available` appointment slots tomorrow
(keep the owner's preferred hour's slot unavailable if you want to show
same-day fallback — set it `Reserved` beforehand).

## Cross-client demo (Owner → API → Agentic → Manager → DB → Owner)

Headline scenario — multi-slot trauma consultation:

- Owner submits: preferred date = tomorrow, preferred time = 10:00,
  symptoms = *"Dog was hit by a vehicle and is limping badly. It is not
  putting weight on the back leg."*
- Consultation Agent (expected): `complexity: complex`,
  `estimatedDurationMinutes: 120`, `requiredSlots: 2`, advisory reason —
  no definitive diagnosis.
- Scheduling Agent: if 10:00–12:00 is not a complete free window it shows
  `Preferred time available: No` + `Fallback: Same-day nearest available
  time` and proposes e.g. 12:00–14:00 with 2 slot IDs. If nothing fits on
  the preferred date it picks the next date (`Next available date`). With
  no window at all the workflow fails safely (`No suitable appointment
  window is currently available…`).
- Manager approves → backend transactionally books BOTH slots.
- Owner refreshes (React consultation list / Flutter Appointments) →
  `Appointment confirmed` chip; appointment shows `12:00 – 14:00`,
  `Duration: 2 hours`.

## Step-by-step

For each step: **Action → Screen/API → What to say → Requirement shown**.

For each step: **Action → Screen/API → What to say → Requirement shown**.

1. **Owner submits consultation** (React: PetOwner → New Consultation)
   → pet + symptoms + preferred slot → Submit.
   → *"A consultation request is a business entity — the AI never creates it."*
   → Req: business workflow, role separation.

2. **Workflow auto-created** → open the consultation → `agentWorkflowStatus`
   chip shows **Created**; `GET /api/agent-workflows/by-consultation/{id}`
   returns the Workflow ID.
   → *"The backend auto-creates a persistent workflow; this ID follows the case forever."*
   → Req: workflow persistence, backend authority.

3. **Manager runs the workflow** → Manager → Agent Workflows → Run.
   → Panel shows the **Supervisor plan table** (steps: agent/type/purpose/requires).
   → *"The Supervisor agent asked the LLM for a plan, validated it deterministically — allow-listed agents only, ordering rules, then routed by code, not by prompt."*
   → Req: planning, structured output, deterministic validation.

4. **Specialists execute** → the panel shows the **10-stage progress**
   (Consultation submitted → … → Completed), the **Owner request** summary,
   the **Consultation assessment** (severity, complexity, estimated
   duration, required slots, recommended examination, reason, confidence)
   and a **Specialist agents** list naming each agent and its task/status
   → status **PendingManagerApproval**.
   → *"The Consultation Agent estimated two hours of work; code — not the LLM —
   turned that into a two-slot requirement."*
   → Req: multi-agent, delegation, tool allow-lists, bounded AI output.

5. **Scheduling proposal + human approval** → the proposal card shows
   Requested vs Proposed window, Required slots, `Preferred time
   available`, the Fallback used and **Why** → Approve with a comment (or
   show Reject/Revision — both require comments).
   → trajectory gains `decision_approved`; backend emits `book_appointment`
   and `AssignConsultationAsync` reserves every slot in the window
   transactionally.
   → *"The LLM cannot approve itself — the gate is a backend endpoint guarded by ClinicManager role, and the backend re-validates slot availability at approval time."*
   → Req: human-in-the-loop, authorization, safe action boundary.

6. **Workflow waits for business event** → status **AwaitingExamination**,
   trajectory shows `awaiting_event`.
   → *"The workflow now parks — it continues only when a REAL business event happens."*
   → Req: stateful workflow, event-driven continuation.

7. **Vet examines** → Veterinarian → record Examination on the booked
   appointment → click **AI Assist / Recommendations**.
   → same Workflow ID resumes (`examination_recorded`); `diagnosis_agent`
   step appears → status **AwaitingPrescription**.
   → *"The vet's own UI action fed the event that continued the graph — additive AI assist."*
   → Req: workflow continuity, event continuation, advisory AI.

8. **Diagnosis → treatment → prescription** → vet saves diagnosis + treatment
   record + prescription with medicines.
   → *"Still normal backend CRUD — validated, authorized, tenant-scoped."*
   → Req: business components.

9. **Inventory plan** → Inventory Officer → prescription → **AI Plan**.
   → `prescription_created` resumes the same workflow; `inventory_agent`
   proposes fulfilment → status **Completed**.
   → Req: 4th specialist, end-of-lifecycle.

9b. **Owner sees confirmation cross-client** → owner re-opens React or the
    Flutter app → Appointments → the booked appointment shows the full
    `start – end` window and Duration (e.g. `10:00 – 12:00`, `2 hours`)
    and the consultation chip reads **Appointment confirmed**.
    → *"Same authoritative ASP.NET data on web and mobile — no client talks
    to the Python service directly."*
    → Req: cross-client consistency, mobile app.

10. **Show the audit trail** → Workflow History: ~33 ordered events —
    `plan_created → delegated → awaiting_decision → decision_approved →
    backend_action_emitted → awaiting_event → examination_recorded →
    plan_reused → delegated → workflow_completed`; approvals table.
    → *"Every decision, wait, and event is persisted — full observability."*
    → Req: observability/audit, persistence.

11. **Infra evidence (30 s)** → `/health` = Healthy/database-reachable;
    `/swagger`; mention Supabase + `AgentWorkflow*` tables; show APK file.
    → Req: deployment evidence, mobile artifact.

Fallbacks if live demo fails: `docs/final/evidence/live-smoke-test.md`
(real workflow `ba29be54`, all 33 events listed) +
`backend/api/tests/e2e/results/latest-run.txt` (91/91) +
`docs/testing/performance-evidence.md`.
