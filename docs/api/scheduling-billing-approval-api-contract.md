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

`ISchedulingService`/`SchedulingService` and `AppointmentsController` are implemented and verified end-to-end against PostgreSQL (see `docs/ai/AI-Usage-Log-Member4.md` Entries 03–04). Note: the actual implemented route prefix is `/api/appointments` (`AppointmentsController`), not `/api/scheduling/slots` as originally planned above; endpoints are `GET /api/appointments`, `GET /api/appointments/{id}`, `GET /api/appointments/mine?status=&from=&to=&petId=` (caller-scoped: vet sees own schedule, owner sees own pets' appointments, CM/Admin see the org list), `POST /api/appointments`, `PUT /api/appointments/{id}`, `DELETE /api/appointments/{id}`, `GET /api/appointments/available-slots`, `POST /api/appointments/check-conflict`. `GET /api/appointments/{id}` also allows the owning PetOwner (ownership checked in the action → 404 for others' pets).

### Consultation-driven scheduling (WorkflowRedesign)

- `POST /api/consultations/{id}/assign` — **ClinicManager + Administrator**. Body `{veterinarianId, date, startTime, endTime, notes?}` (`AssignVeterinarianRequest`). Creates the `AppointmentSlot` (Reserved) + `Confirmed` `Appointment` linked to the request (`Appointment.ConsultationRequestId`, `Type` = `Initial`/`FollowUp`) and moves the request to `AppointmentConfirmed` (+ status-history row), all in one unit of work. 404 unknown request/vet · 409 inactive vet, overlap, or request not `Submitted`/`Processing`.
- `POST /api/consultations/follow-up` — **Veterinarian + Administrator**. Body `{petId, examinationId?, preferredDate, reason, notes?}` (`CreateFollowUpRequest`). Creates a `RequestType=FollowUp` request (`Submitted`) attributed to the attending vet (`RequestedByVeterinarianId`, resolved via `Veterinarian.UserId` → logged-in user; unlinked vet → 403). Confirmed through the same assign endpoint → `FollowUp` appointment.

### Booking rules & availability (BookingRules)

Operating hours are 09:00–18:00 as code constants (`BookingRules`), yielding nine fixed one-hour slots per day; `EndTime` is always `StartTime + 1h` computed server-side. Consultation create/update/follow-up require organization + date + hour-aligned slot start; past dates are rejected. Org-level slot capacity is checked at create and submit — a full slot returns 409 "This appointment slot is no longer available. Please select another time." The assign path enforces the same 1-hour rule plus veterinarian overlap (409), and a unique `(VeterinarianId, Date, StartTime)` index on `AppointmentSlot` guards concurrent assigns (SQLSTATE 23505 → 409). `ConsultationRequest.OrganizationId` (nullable, migration `20260927070805_BookingRules`) carries the clinic choice; staff consultation reads are org-scoped.

- `GET /api/consultations/availability?organizationId&date[&veterinarianId]` — any authenticated; `{date,isPast,slots:[{start,end,available,availableVeterinarianIds}]}`. `veterinarianId` narrows the check to that vet (manager assign flow).
- `GET /api/consultations/availability/month?organizationId&year&month` — any authenticated; `[{date,available,fullyBooked,isPast}]` per day of the month.
- `GET /api/lookups/organizations` — any authenticated; Active orgs `{id,name,city,address,latitude,longitude}` — the marker source for the owner map.

### Clinic locator (OrganizationLocation)

- `GET /api/consultations/nearby-clinics?latitude&longitude&radiusKm=50` — any authenticated; `[{id,name,address,city,latitude,longitude,distanceKm}]` for orgs that are `Active` + `IsActive` with both coordinates set, Haversine straight-line distance (2dp), nearest first; 400 invalid ranges.
- `GET /api/consultations/nearest-clinic?latitude&longitude` — any authenticated; the first result of the unbounded nearby query (`{id,name,address,latitude,longitude,distanceKm}`) or 404 `{"message":"No active clinics with a location were found."}`. The previous hardcoded fake-clinic list was removed — it now reads real `Organization` rows (migration `20260927081008_OrganizationLocation` adds `Latitude`/`Longitude`; `POST /api/auth/register/organization` accepts them optionally, both-or-neither and range-validated).

## Billing

**Application layer: Implemented**
**API controller: Implemented**

- `GET /api/quotations` (Vet, CM, IO, Admin — org-scoped)
- `GET /api/quotations/mine` (PetOwner — own pets' bills)
- `GET /api/quotations/{id}` (Vet, CM, IO, Admin + owning PetOwner → 404 for others)
- `POST /api/quotations/{id}/mark-paid` (**InventoryOfficer + Administrator only** — Vet/CM → 403; `Finalised`+`Pending` → `Paid` + `PaidAt`/`PaidByUserId`; else 409)
- `POST /api/quotations`
- `PUT /api/quotations/{id}`
- `POST /api/quotations/{id}/submit`
- `POST /api/quotations/{id}/calculate`
- `POST /api/quotations/{id}/finalize`

Business operations: (a) calculate complete quotation and compare against the owner's budget; (b) the Quotation entity doubles as the **auto-generated bill** — `BillingService.GenerateOrRefreshBillForExaminationAsync` builds/refreshes it from an appointment-linked examination (one `Examination` line = `VeterinarianCharge`, one `Medicine` line per `Issued` prescription at the medicine's unit price), `Status=Finalised`, `PaymentStatus=Pending`, derived `InvoiceNumber = INV-<8-hex>` on `QuotationResponse` alongside pet/owner/vet/examination display fields. A `Paid` bill is never mutated.

`IBillingService`/`BillingService`, Billing DTOs, `QuotationValidator`, `IQuotationRepository`/`QuotationRepository`, and `QuotationsController` are implemented (see `docs/ai/AI-Usage-Log-Member4.md` Entries 05–06 for the original build; the billing workflow was extended in the WorkflowRedesign step).

## Approval

**Application layer: Implemented**
**API controller: Implemented**

- `GET /api/approvals/pending`
- `GET /api/approvals/{id}`
- `POST /api/approvals/{id}/approve`
- `POST /api/approvals/{id}/reject`
- `POST /api/approvals/{id}/revision`
- `GET /api/approvals/{id}/history`

Business operation: high-impact execution stays blocked until an authorized Clinic Manager decision. Implemented in `IApprovalService`/`ApprovalService` + `ApprovalsController` (Entries 07–08): lazy `Pending` approval-row provisioning, decision endpoints are **ClinicManager only**, every decision appends an `ApprovalHistory` row, and `ReviewedBy` is resolved from the JWT tenant identity (request-body value is fallback only). This flow gates **manually drafted** quotations; auto-generated bills arrive `Finalised` and do not pass through approval.

## Medicine requests (WorkflowRedesign)

- `GET /api/prescriptions/requests?status=` — Vet, CM, IO, Admin; the queue of `Prescription` rows with `RequestStatus` Pending/Issued/Unavailable.
- `POST /api/prescriptions/{id}/issue` — **InventoryOfficer + Administrator**. Reserves + dispenses atomically via the existing stock rules (`IInventoryService` — insufficient stock → `InventoryConflictException` 409, never negative; a failed dispense releases the reservation). Sets `Issued` + `ReservationId` + `ProcessedByUserId`/`ProcessedAt`, then refreshes the appointment's bill.
- `POST /api/prescriptions/{id}/unavailable` — **IO + Administrator**. Body `{reason}` (required → 400 if empty). Sets `Unavailable` + `UnavailableReason` + processed audit fields, then refreshes the bill. Both reject non-`Pending` requests with 409.

## Manager veterinarian history (WorkflowRedesign)

- `GET /api/manager/veterinarians` — **ClinicManager only**; active vets in the caller's org.
- `GET /api/manager/veterinarians/{id}/history?from=&to=` — **ClinicManager only**; counts + lists for appointments (completed/upcoming/cancelled), examinations (initial/follow-up), prescriptions, medicine requests (pending/issued/unavailable), and bills (totals, paid/pending). 404 for a vet outside the org.

## AI advisory boundary

The earlier mock `/api/agent-workflows*` monitoring endpoints were never implemented on the API and the mock UI was removed. The implemented advisory surface is four agent-proxied GET endpoints (see `api-reference.md` → AI-related endpoints):

- `GET /api/consultations/{id}/analysis` — ClinicManager, Administrator (consultation triage)
- `GET /api/consultations/{id}/scheduling-plan` — ClinicManager, Administrator (scheduling + quotation proposal)
- `GET /api/examinations/{id}/recommendations` — Veterinarian, ClinicManager, Administrator (diagnosis assist)
- `GET /api/prescriptions/treatment/{treatmentRecordId}/inventory-plan` — InventoryOfficer, Administrator (stock/batch plan)

All four proxy to the internal `agentic-service` via `IAgenticClient` and are advisory-only: they persist nothing and perform no workflow action.
