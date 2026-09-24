# Component Boundaries

## 1. Scheduling, Billing & Approval Management Scope
- Veterinarian availability and appointment slots
- Conflict-free appointment slot validation
- Quotations and line-item billing
- Quote total and budget validation
- Manager approval/reject/revision workflow
- Approval validation summary UI
- AI workflow monitoring UI

## 2. Medicine & Inventory Management Scope
- Pharmaceutical catalog and SKU management (Medicines)
- Supplier management and procurement directory
- Multi-batch tracking with expiration and receipt dates
- First-Expiry-First-Out (FEFO) dispensing order
- Atomic stock reservations with race condition protection (HTTP 409 Conflict)
- Complete transaction ledger and audit trail (`InventoryTransactions`)
- Role-based authorization (`InventoryOfficer`, `Veterinarian`, `ClinicManager`, `SuperAdmin`)
- Production React dashboard for medicine catalog, low stock alerts, FEFO batch inspection, and supplier directory

## 3. Excluded Scope (Deferred / External)
- Background AI LLM generation agents (AI workflow screens are monitoring UI only)
- Direct cross-component database coupling (integration via clean service/API boundaries)

Note: the Flutter mobile client **is implemented** — scheduling/billing/approval screens under `frontend/mobile` (see `docs/testing/flutter-testing.md`).

## 4. Integration Expectations
- React consumes the ASP.NET Core API via `apiRequest` client with Bearer JWT tokens.
- Backend remains authoritative for business validation, atomic concurrency control, and transactional persistence.
- PostgreSQL database stores all normalized entities with check constraints, foreign keys, and indexes.

