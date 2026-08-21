# API contract

Final planned endpoints for the Scheduling, Billing and Approval modules. See `docs/database/scheduling-billing-approval-domain-model.md` for the underlying entities, statuses and business rules.

## Scheduling

**Application layer: Implemented**
**API controller: Implemented**

- `GET /api/scheduling/slots`
- `GET /api/scheduling/slots/{id}`
- `POST /api/scheduling/slots`
- `PATCH /api/scheduling/slots/{id}/status`
- `GET /api/scheduling/slots/conflicts`

Business operation: reject overlapping veterinarian/date/time slots.

`ISchedulingService`/`SchedulingService` and `AppointmentsController` are implemented and verified end-to-end against PostgreSQL (see `docs/ai/AI-Usage-Log-Member4.md` Entries 03–04). Note: the actual implemented route prefix is `/api/appointments` (`AppointmentsController`), not `/api/scheduling/slots` as originally planned above; endpoints are `GET /api/appointments`, `GET /api/appointments/{id}`, `POST /api/appointments`, `PUT /api/appointments/{id}`, `DELETE /api/appointments/{id}`, `GET /api/appointments/slots/available`, `POST /api/appointments/conflicts`.

## Billing

**Application layer: Implemented**
**API controller: Pending**

- `GET /api/quotations`
- `GET /api/quotations/{id}`
- `POST /api/quotations`
- `PUT /api/quotations/{id}`
- `POST /api/quotations/{id}/submit`
- `POST /api/quotations/{id}/calculate`

Business operation: calculate complete quotation and compare against the owner's budget.

`IBillingService`/`BillingService`, Billing DTOs, `QuotationValidator`, and `IQuotationRepository`/`QuotationRepository` are implemented and unit-tested in `PetCare.Application`/`PetCare.Infrastructure` (see `docs/ai/AI-Usage-Log-Member4.md` Entry 05). No `QuotationsController` exists yet, so these endpoints are not yet reachable over HTTP.

## Approval

**Application layer: Pending**
**API controller: Pending**

- `GET /api/approvals/pending`
- `GET /api/approvals/{id}`
- `POST /api/approvals/{id}/approve`
- `POST /api/approvals/{id}/reject`
- `POST /api/approvals/{id}/revision`
- `GET /api/approvals/{id}/history`

Business operation: high-impact execution stays blocked until an authorized Clinic Manager decision.

Not started. `Approval`/`ApprovalHistory` entities and EF Core configurations exist (`PetCare.Domain`/`PetCare.Infrastructure`, Entry 02), but no `IApprovalService`, DTOs, validators, repository, or controller have been implemented yet.

## AI UI boundary

- `GET /api/agent-workflows`
- `GET /api/agent-workflows/{id}`
- `GET /api/agent-workflows/{id}/execution-summary`

No AI execution endpoint is called by the current UI.
