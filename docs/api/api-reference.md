# API Reference

Complete endpoint reference for the PetCare AI API, generated from the actual controllers in `backend/api/src/PetCare.Api/Controllers/`. Base URL in development: `http://localhost:5019`.

## Conventions

- **Auth:** JWT bearer — `Authorization: Bearer <token>` from `POST /api/auth/login`. Claims: `sub` (user id), `email`, `name`, `role`.
- **Tenant scoping:** staff requests are filtered to the caller's `OrganizationId` automatically; cross-org ids fail as 404. `Administrator` is unscoped. `PetOwner` requests are governed by server-side ownership checks — any client-supplied owner/pet ids are overwritten from the JWT identity.
- **Status codes:** 400 validation · 401 unauthenticated/invalid credentials · 403 wrong role or pending organization · 404 not found / not owned / out of scope · 409 business conflict (double-reserve, double-dispense, vet overlap, illegal status transition).
- Errors return a problem-details JSON body: `{ "title": ..., "errors": ... }`.

## Role legend

`Owner` = PetOwner · `Vet` = Veterinarian · `CM` = ClinicManager · `IO` = InventoryOfficer · `Admin` = Administrator

---

## Authentication — `api/auth`

| Method | Route | Auth | Body → Response | Notes |
|---|---|---|---|---|
| POST | `/login` | Public | `LoginRequest` → `LoginResponse` | 401 on bad credentials/inactive account; 403 `OrganizationPending` for staff of non-Active orgs |
| POST | `/register`, `/register/pet-owner` | Public | `RegisterPetOwnerRequest` → `CurrentUserResponse` | Creates `User` + linked `PetOwner` atomically; 201 |
| POST | `/register/organization` | Public | `RegisterOrganizationRequest` → `CurrentUserResponse` | Creates `Organization` (Pending) + initial `ClinicManager` atomically; 201; optional `latitude`/`longitude` (both-or-neither; lat ∈ [-90,90], lng ∈ [-180,180]) stores the clinic's map location |
| PUT | `/change-password` | Any authenticated | `ChangePasswordRequest` | Clears `MustChangePassword` |
| PUT | `/profile` | Any authenticated | `UpdateProfileRequest` → `CurrentUserResponse` | |

## Pet Owners — `api/petowners` (alias `api/owners`)

| Method | Route | Roles | Body | Notes |
|---|---|---|---|---|
| POST | `/` | Owner, CM, Admin | `CreatePetOwnerDto` | Owner's own `UserId` bound server-side; client `ownerId`/user fields ignored |
| GET | `/` | Owner, Vet, CM, Admin | — | Owner sees only own profile |
| GET | `/{id}` | Owner, Vet, CM, Admin | — | Owner: own only |

## Pets — `api/pets`

| Method | Route | Roles | Body | Notes |
|---|---|---|---|---|
| POST | `/` | Owner, CM, Admin | `CreatePetDto` | `OwnerId` overwritten from JWT — verified live |
| GET | `/` | Owner, Vet, CM, Admin | `?includeArchived=true` | Active pets by default; owner: own pets only |
| GET | `/{id}` | Owner, Vet, CM, Admin | — | Owner: own only; archived pets still readable |
| GET | `/owner/{ownerId}` | Owner, Vet, CM, Admin | `?includeArchived=true` | Owner: own `ownerId` only |
| PUT | `/{id}` | Owner, CM, Admin | `UpdatePetDto` | Owner: own only |
| POST | `/{id}/archive` | Owner, CM, Admin | — | Removes pet from the active list; all history preserved; 400 if already archived |
| POST | `/{id}/restore` | Owner, CM, Admin | — | Returns an archived pet to the active list; 400 if not archived |
| DELETE | `/{id}` | Owner, CM, Admin | — | Owner: own only; **only when the pet has no protected history** (consultations, appointments, examinations → diagnoses → treatment records → prescriptions) — 400 "archive it instead" otherwise |

**Pet lifecycle:** `Active → Remove (archive) → Archived → Restore → Active`. Archived pets keep every historical record and remain readable, but cannot enter new workflows (consultation requests, appointments, follow-ups, direct examinations). Permanent delete is reserved for pets with zero dependent business records.

## Consultation Requests — `api/consultations`

| Method | Route | Roles | Body | Notes |
|---|---|---|---|---|
| POST | `/` | Owner, CM, Admin | `CreateConsultationRequestDto` | Pet/owner identity bound server-side; **organizationId + date + hour-aligned slot start are mandatory** (400 otherwise); the org's slot capacity is checked → 409 "This appointment slot is no longer available. Please select another time." |
| POST | `/{id}/assign` | **CM, Admin** | `AssignVeterinarianRequest` → `AppointmentResponse` | `{veterinarianId, date, startTime, endTime, notes?}` — creates the slot + `Confirmed` appointment (`Type` Initial/FollowUp, `ConsultationRequestId` link), request → `AppointmentConfirmed` + history row; 409 on overlap/inactive vet/illegal status |
| POST | `/follow-up` | **Vet, Admin** | `CreateFollowUpRequest` → `ConsultationRequestDto` (201) | `{petId, examinationId?, preferredDate, reason, notes?}` — `RequestType=FollowUp`, `Submitted`, `RequestedByVeterinarianId` resolved from JWT |
| GET | `/` | Owner, CM, Admin | — | Owner: own only; vets reach consultations through appointments/examinations |
| GET | `/owner/{ownerId}` | Owner, CM, Admin | — | |
| GET | `/{id}` | Owner, CM, Admin | — | |
| GET | `/{id}/analysis` | **CM, Admin** | — → `ConsultationAnalysisDto` | Advisory AI triage of the request (advisory only — see AI section) |
| GET | `/{id}/scheduling-plan` | **CM, Admin** | — → `SchedulingPlanDto` | Advisory AI appointment/quotation proposal (advisory only — see AI section) |
| GET | `/{id}/history` | Owner, CM, Admin | — | Status-history entries |
| PUT | `/{id}` | Owner, CM, Admin | `UpdateConsultationRequestDto` | |
| POST | `/{id}/submit` | Owner, CM, Admin | — | Status transition |
| PATCH | `/{id}/cancel` | Owner, CM, Admin | — | Status transition |
| GET | `/nearest-clinic?latitude&longitude` | Any authenticated | `?latitude&longitude` → `{id,name,address,latitude,longitude,distanceKm}` | Real nearest active org with coordinates (Haversine); 400 invalid coords; 404 when no located clinic exists |
| GET | `/nearby-clinics?latitude&longitude&radiusKm=50` | Any authenticated | `?latitude&longitude&radiusKm` → `NearbyClinicResponse[]` | Active + `IsActive` orgs with coordinates only; `[{id,name,address,city,latitude,longitude,distanceKm}]` sorted nearest-first; 400 invalid coords or `radiusKm <= 0` |
| GET | `/availability?organizationId&date[&veterinarianId]` | Any authenticated | → `{date,isPast,slots:[{start,end,available,availableVeterinarianIds}]}` | Nine fixed 1-hour slots (09:00–18:00) for one org/day; `veterinarianId` narrows to that vet (assign flow) |
| GET | `/availability/month?organizationId&year&month` | Any authenticated | → `[{date,available,fullyBooked,isPast}]` | Per-day availability overview for the booking calendar |
| GET | `/validate-ownership` | Owner, CM, Admin | — | Ownership probe |

## Examinations — `api/examinations`

| Method | Route | Roles | Body | Notes |
|---|---|---|---|---|
| GET | `/` | Vet, CM, Admin | — | Org-scoped |
| GET | `/{id}` | Owner (own) + Vet, CM, Admin | — | |
| GET | `/pet/{petId}` | Owner (own) + Vet, CM, Admin | — | |
| POST | `/` | Vet, Admin | `CreateExaminationDto` | Accepts `appointmentId` + `veterinarianCharge` — completing an appointment marks it + slot `Completed` and inherits pet/consultation links; Vet caller's own profile forced server-side via `Veterinarian.UserId` (client `VeterinarianId` ignored; unlinked vet → 403) |
| PUT | `/{id}` | Vet, Admin | `UpdateExaminationDto` | |
| DELETE | `/{id}` | Vet, Admin | — | |
| GET | `/{id}/recommendations` | Vet, CM, Admin | — → `TreatmentRecommendationDto` | Advisory AI diagnosis assist via the agentic service (see AI section) |

## Diagnoses — `api/diagnoses`

| Method | Route | Roles | Body | Notes |
|---|---|---|---|---|
| GET | `/` | Vet, CM, Admin | — | |
| GET | `/{id}` | Owner (own) + Vet, CM, Admin | — | |
| GET | `/examination/{examinationId}` | Owner (own) + Vet, CM, Admin | — | |
| POST | `/` | Vet, Admin | `CreateDiagnosisDto` | 1:1 with examination (unique index) |
| PUT | `/{id}` | Vet, Admin | `UpdateDiagnosisDto` | |
| DELETE | `/{id}` | Vet, Admin | — | |

## Treatment Records — `api/treatmentrecords`

| Method | Route | Roles | Body | Notes |
|---|---|---|---|---|
| GET | `/` | Vet, CM, Admin | — | |
| GET | `/{id}` | Owner (own) + Vet, CM, Admin | — | |
| GET | `/diagnosis/{diagnosisId}` | Owner (own) + Vet, CM, Admin | — | |
| POST | `/` | Vet, Admin | `CreateTreatmentRecordDto` | |
| PUT | `/{id}` | Vet, Admin | `UpdateTreatmentRecordDto` | |
| PATCH | `/{id}/status` | Vet, Admin | `UpdateTreatmentStatusDto` | |
| DELETE | `/{id}` | Vet, Admin | — | |

## Prescriptions — `api/prescriptions`

| Method | Route | Roles | Body | Notes |
|---|---|---|---|---|
| GET | `/` | Vet, CM, Admin | — | |
| GET | `/{id}` | Owner (own) + Vet, CM, Admin | — | |
| GET | `/treatment/{treatmentRecordId}` | Owner (own) + Vet, CM, Admin | — | |
| POST | `/` | Vet, Admin | `CreatePrescriptionDto` | Accepts `quantity`, `frequency`, `instructions`; `RequestStatus` starts `Pending` = the medicine request |
| GET | `/requests?status=` | Vet, CM, IO, Admin | — | Medicine-request queue (Pending/Issued/Unavailable), newest first |
| GET | `/treatment/{treatmentRecordId}/inventory-plan` | **IO, Admin** | — → `InventoryPlanDto` | Advisory AI stock/batch fulfilment plan (see AI section) |
| POST | `/{id}/issue` | **IO, Admin** | — → `PrescriptionResponseDto` | Reserve + dispense atomically via stock rules; 409 insufficient stock (never negative); sets `Issued` + `ReservationId` + processed audit fields, then refreshes the bill |
| POST | `/{id}/unavailable` | **IO, Admin** | `{reason}` (required) → `PrescriptionResponseDto` | `RequestStatus` → `Unavailable`; 400 empty reason, 409 non-Pending |
| DELETE | `/{id}` | Vet, Admin | — | |

## Medicines — `api/medicines`

| Method | Route | Roles | Body | Notes |
|---|---|---|---|---|
| GET | `/` | Vet, CM, IO, Admin | — | Org-scoped |
| GET | `/low-stock` | CM, IO, Admin | — | |
| GET | `/expiring` | CM, IO, Admin | — | FEFO expiry window |
| GET | `/{id}` | Vet, CM, IO, Admin | — | |
| POST | `/` | IO, Admin | `CreateMedicineRequest` | |
| POST | `/{id}/stock-in` | IO, Admin | `ReceiveStockRequest` | Batch receipt; `performedByUserId` from JWT |
| GET | `/{id}/batches` | CM, IO, Admin | — | FEFO-ordered |
| GET | `/{id}/transactions` | CM, IO, Admin | — | Ledger |

## Suppliers — `api/suppliers`

| Method | Route | Roles | Body | Notes |
|---|---|---|---|---|
| GET | `/`, `/{id}` | IO, CM, Admin | — | Org-scoped |
| POST | `/` | IO, Admin | `CreateSupplierRequest` | |

## Medicine Reservations — `api/medicine-reservations`

| Method | Route | Roles | Body | Notes |
|---|---|---|---|---|
| GET | `/` | Vet, IO, CM, Admin | — | Org-scoped |
| POST | `/` | Vet, IO, Admin | `ReserveMedicineRequest` | Atomic stock decrement; 409 on insufficient stock; `requestedByUserId` from JWT |
| POST | `/{id}/cancel` | Vet, IO, Admin | — | Atomic `Reserved→Cancelled` transition; 409 on double-cancel |
| POST | `/{id}/dispense` | IO, Admin | — | Atomic `Reserved→Dispensed`; 409 on double-dispense |

## Scheduling — `api/appointments`

| Method | Route | Roles | Body | Notes |
|---|---|---|---|---|
| GET | `/` | Vet, CM, Admin | — | Org-scoped (via Veterinarian) |
| GET | `/mine?status=&from=&to=&petId=` | Owner, Vet, CM, Admin | — | Vet: own schedule; Owner: own pets' appointments; CM/Admin: org list |
| GET | `/{id}` | Owner (own) + Vet, CM, Admin | — | Owner: own pets only → 404 otherwise |
| POST | `/` | CM, Admin | `CreateAppointmentRequest` | `PetId` is the string Pet id (`PET-…`); 409 on vet double-booking |
| PUT | `/{id}` | CM, Admin | `UpdateAppointmentRequest` | |
| DELETE | `/{id}` | CM, Admin | — | |
| GET | `/available-slots` | Vet, CM, Admin | — | Query params |
| POST | `/check-conflict` | Vet, CM, Admin | `ConflictCheckRequest` | Overlap check without saving |

## Quotations / Billing — `api/quotations`

| Method | Route | Roles | Body | Notes |
|---|---|---|---|---|
| GET | `/` | Vet, CM, IO, Admin | — | Org-scoped; bills generated from examinations live here too (`Status=Finalised`, `PaymentStatus`, derived `InvoiceNumber = INV-<8-hex>`) |
| GET | `/mine` | **Owner only** | — | The caller's bills for their own pets |
| GET | `/{id}` | Owner (own) + Vet, CM, IO, Admin | — | Owner: own pets only → 404 otherwise |
| POST | `/{id}/mark-paid` | **IO, Admin** | — → `QuotationResponse` | `Finalised` + `Pending` → `Paid` + `PaidAt`/`PaidByUserId`; Vet/CM → 403; else 409 |
| POST | `/` | CM, Admin | `CreateQuotationRequest` | Totals recomputed server-side |
| PUT | `/{id}` | CM, Admin | `UpdateQuotationRequest` | Draft only — approved/finalized are read-only |
| POST | `/{id}/calculate` | CM, Admin | — | |
| POST | `/{id}/submit` | CM, Admin | — | Budget check; 409 illegal transition |
| POST | `/{id}/finalize` | CM, Admin | — | |

## Approvals — `api/approvals`

| Method | Route | Roles | Body | Notes |
|---|---|---|---|---|
| GET | `/pending`, `/{id}`, `/{id}/history` | Vet, CM, Admin | — | |
| POST | `/{id}/approve` | **CM only** | `ApproveRequest` | `ReviewedBy` resolved from the JWT tenant identity (request-body value is fallback only) |
| POST | `/{id}/reject` | **CM only** | `RejectRequest` | Comment required |
| POST | `/{id}/revision` | **CM only** | `RequestRevisionRequest` | Comment required |

## Administration — `api/admin` — **Administrator only**

| Method | Route | Body → Response | Notes |
|---|---|---|---|
| GET | `/users` | — → `AdminUserResponse[]` | All platform users — includes manager-created staff |
| PATCH | `/users/{id}/status` | `UpdateUserStatusRequest` → `AdminUserResponse` | Cannot deactivate self |
| GET | `/organizations` | — → `AdminOrganizationResponse[]` | |
| PATCH | `/organizations/{id}/status` | `UpdateOrganizationStatusRequest` → `AdminOrganizationResponse` | Active/Rejected/Suspended/Inactive; reason required for Rejected/Suspended |
| GET | `/roles` | — → `string[]` | Role catalog |
| GET | `/system` | — → `AdminSystemInfoResponse` | Counts by role/status |

The Administrator manages users and organizations but **does not create staff accounts** — that is the ClinicManager's job (below).

## Manager self-administration — `api/manager` — **ClinicManager only**

| Method | Route | Body → Response | Notes |
|---|---|---|---|
| GET | `/veterinarians` | — → `ManagerVeterinarianResponse[]` | **Active** veterinarians in the caller's own org |
| GET | `/veterinarians/{id}/history?from=&to=` | — → `VeterinarianHistoryResponse` | Per-vet work history: appointment counts (completed/upcoming/cancelled) + lists, examinations (initial/follow-up), prescriptions, medicine-request counts (pending/issued/unavailable), bills (totals, paid/pending); 404 vet not in org |
| **POST** | **`/users/veterinarians`** | `ManagerCreateStaffRequest` → `CreatedStaffAccountResponse` | Creates `Role=Veterinarian` inside the caller's own org **plus a linked `Veterinarian` profile row** (`UserId` bound) |
| **POST** | **`/users/inventory-officers`** | `ManagerCreateStaffRequest` → `CreatedStaffAccountResponse` | Same, `Role=InventoryOfficer` |

`ManagerCreateStaffRequest`: `{ firstName, lastName, email, phoneNumber? }` — **no role and no `organizationId` field at all**: the role is fixed by the endpoint and the organization is resolved server-side from the manager's JWT identity (`User.OrganizationId` via `ITenantContext`). A manager can never create an account in another organization.

- `MustChangePassword=true`, `Active=true`; response carries a one-time `temporaryPassword`
- 400 duplicate email / validation · 401 unauthenticated · 403 non-ClinicManager, unscoped manager, or non-Active org

## Lookups — `api/lookups`

| Method | Route | Roles | Notes |
|---|---|---|---|
| GET | `/pets` | Vet, CM, Admin | Compact pet list for pickers |
| GET | `/medicines` | Vet, CM, IO, Admin | |
| GET | `/organizations` | Any authenticated | Active orgs an owner can book at — `{id,name,city,address,latitude,longitude}`; coordinates are `null` until a clinic registers a location (marker source for the owner map) |

## AI-related endpoints

The backend exposes four **advisory** endpoints that proxy to the internal `agentic-service` (FastAPI + LangGraph + Gemini) via `IAgenticClient` (typed `HttpClient` configured by `AgenticService:BaseUrl` + `AgenticService:InternalKey`). All are read-only recommendations — nothing is persisted and no business action is taken automatically.

| Method | Route | Roles | Response | Agent |
|---|---|---|---|---|
| GET | `/api/consultations/{id}/analysis` | CM, Admin | `ConsultationAnalysisDto` | Consultation triage (priority, type, concerns, suggested preparation) |
| GET | `/api/consultations/{id}/scheduling-plan` | CM, Admin | `SchedulingPlanDto` | Scheduling plan (recommended vet/slot, quotation draft) |
| GET | `/api/examinations/{id}/recommendations` | Vet, CM, Admin | `TreatmentRecommendationDto` | Diagnosis assist (differential diagnoses, medicine suggestions resolved to real catalogue ids on unique org-scoped match) |
| GET | `/api/prescriptions/treatment/{treatmentRecordId}/inventory-plan` | IO, Admin | `InventoryPlanDto` | Inventory plan (stock/batch fulfilment recommendation) |

**Contract:** each response carries a `source` field — `"agent"` on success or `"unavailable"`/safe fallback on timeout, LLM failure, or validation failure. The agentic service is internal-only: callers hit the ASP.NET API with their JWT; the API adds `X-Internal-Key` and forwards the caller's bearer token so agent data reads inherit the caller's role/organization scope. The service-to-service `POST /api/agents/*` endpoints are documented in `agentic-service/README.md` and are never exposed to browsers.
