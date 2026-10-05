# Component Boundaries

## 1. Scheduling, Billing & Approval Management Scope
- Veterinarian availability and appointment slots
- Conflict-free appointment slot validation
- Consultation-request → veterinarian assignment (`POST /api/consultations/{id}/assign`) producing slot + Confirmed appointment (`ConsultationWorkflowService`)
- Veterinarian follow-up consultation requests (`POST /api/consultations/follow-up`)
- Caller-scoped appointment views (`GET /api/appointments/mine`; owning PetOwner on `/{id}`)
- Quotations and line-item billing — the Quotation doubles as the auto-generated bill (`BillingService.GenerateOrRefreshBillForExaminationAsync`: vet charge + Issued prescriptions)
- Payment recording (`POST /api/quotations/{id}/mark-paid` — InventoryOfficer/Administrator)
- Medicine-request queue on prescriptions (`MedicineRequestService`: `GET /requests`, `issue`, `unavailable`) — stock rules stay in the inventory component's `IInventoryService`
- Manager veterinarian history (`GET /api/manager/veterinarians*`)
- Fixed-slot booking rules (09:00–18:00, nine 1-hour slots, `BookingRules`) with org/day/month availability endpoints (`GET /api/consultations/availability*`)
- Clinic locator on real organization coordinates (`IClinicLocatorService` → `GET /api/consultations/nearby-clinics`, `nearest-clinic`; `Organization.Latitude`/`Longitude`)
- Google Maps integration in React (`lib/googleMaps.ts`, `LocationPickerMap`, `ClinicMap`) — optional, script-loader based, no new npm deps; a skippable retry notice (registration) and a clinic-card list (booking) keep both flows unblocked
- Quote total and budget validation
- Manager approval/reject/revision workflow (manual-quotation path)
- Approval validation summary UI
- Advisory AI workflow panels (manager consultation analysis + scheduling plan, vet diagnosis assist, inventory-officer stock plan)

## 2. Medicine & Inventory Management Scope
- Pharmaceutical catalog and SKU management (Medicines)
- Supplier management and procurement directory
- Multi-batch tracking with expiration and receipt dates
- First-Expiry-First-Out (FEFO) dispensing order
- Atomic stock reservations with race condition protection (HTTP 409 Conflict)
- Complete transaction ledger and audit trail (`InventoryTransactions`)
- Role-based authorization (`InventoryOfficer`, `Veterinarian`, `ClinicManager`, `SuperAdmin`)
- Production React dashboard for medicine catalog, low stock alerts, FEFO batch inspection, and supplier directory

## 3. Advisory AI (separate service, non-authoritative)
- `agentic-service/` (FastAPI + LangGraph + Gemini) implements four advisory agents — consultation triage, diagnosis, scheduling plan, inventory plan — all wired through `IAgenticClient` behind staff-only GET endpoints. They analyse and recommend only; they never create, modify, approve, dispense, or bill.
- A **Supervisor/Planner** (`agentic-service/supervisor/`) orchestrates the four agents on an explicit LangGraph `StateGraph` with deterministic validation and an `interrupt()` human-approval gate. The gate is resumed only by an authenticated ClinicManager decision posted to `api/agent-workflows`; the emitted `book_appointment` action is executed by the backend (`ConsultationWorkflowService.AssignConsultationAsync`), never by Python. Workflow state is persisted in `AgentWorkflow*` tables (see `docs/agentic/orchestration-workflow.md`).
- Direct cross-component database coupling remains excluded (integration via clean service/API boundaries); the Python service holds no DB credentials and never writes business data.

Note: the Flutter mobile client **is implemented** as the PetOwner-only app under `frontend/mobile` — register/login, pet management, clinic-map consultation booking, appointments, bills and profile. Flutter is used as the Pet Owner mobile application. Pet Owners can register/login, manage their pets, find active PetCare clinics on a map, submit consultation requests, view appointments and follow-up appointments, and view their bills. Clinic Manager, Veterinarian, Inventory Officer and Administrator workflows remain in the React staff application. Google Maps is used for map visualization, clinic selection and directions. Bookable clinics are the active organizations registered in the PetCare system. (See `docs/testing/flutter-testing.md`.)

## 4. Integration Expectations
- React consumes the ASP.NET Core API via `apiRequest` client with Bearer JWT tokens.
- Backend remains authoritative for business validation, atomic concurrency control, and transactional persistence.
- PostgreSQL database stores all normalized entities with check constraints, foreign keys, and indexes.

