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
| POST | `/register/organization` | Public | `RegisterOrganizationRequest` → `CurrentUserResponse` | Creates `Organization` (Pending) + initial `ClinicManager` atomically; 201 |
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
| GET | `/` | Owner, Vet, CM, Admin | — | Owner: own pets only |
| GET | `/{id}` | Owner, Vet, CM, Admin | — | Owner: own only |
| GET | `/owner/{ownerId}` | Owner, Vet, CM, Admin | — | Owner: own `ownerId` only |
| PUT | `/{id}` | Owner, CM, Admin | `UpdatePetDto` | Owner: own only |
| DELETE | `/{id}` | Owner, CM, Admin | — | Owner: own only |

## Consultation Requests — `api/consultations`

| Method | Route | Roles | Body | Notes |
|---|---|---|---|---|
| POST | `/` | Owner, CM, Admin | `CreateConsultationRequestDto` | Pet/owner identity bound server-side |
| GET | `/` | Owner, Vet, CM, Admin | — | Owner: own only |
| GET | `/owner/{ownerId}` | Owner, Vet, CM, Admin | — | |
| GET | `/{id}` | Owner, Vet, CM, Admin | — | |
| GET | `/{id}/history` | Owner, Vet, CM, Admin | — | Status-history entries |
| PUT | `/{id}` | Owner, CM, Admin | `UpdateConsultationRequestDto` | |
| POST | `/{id}/submit` | Owner, CM, Admin | — | Status transition |
| PATCH | `/{id}/cancel` | Owner, CM, Admin | — | Status transition |
| GET | `/nearest-clinic` | Any authenticated | — | Utility lookup |
| GET | `/validate-ownership` | Owner, Vet, CM, Admin | — | Ownership probe |

## Examinations — `api/examinations`

| Method | Route | Roles | Body | Notes |
|---|---|---|---|---|
| GET | `/` | Vet, CM, Admin | — | Org-scoped |
| GET | `/{id}` | Owner (own) + Vet, CM, Admin | — | |
| GET | `/pet/{petId}` | Owner (own) + Vet, CM, Admin | — | |
| POST | `/` | Vet, Admin | `CreateExaminationDto` | ConsultationRequest + vet must be in-scope |
| PUT | `/{id}` | Vet, Admin | `UpdateExaminationDto` | |
| DELETE | `/{id}` | Vet, Admin | — | |
| GET | `/{id}/recommendations` | Vet, CM, Admin | — | |

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
| POST | `/` | Vet, Admin | `CreatePrescriptionDto` | |
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
| GET | `/`, `/{id}` | Vet, CM, Admin | — | Org-scoped (via Veterinarian) |
| POST | `/` | CM, Admin | `CreateAppointmentRequest` | `PetId` is the string Pet id (`PET-…`); 409 on vet double-booking |
| PUT | `/{id}` | CM, Admin | `UpdateAppointmentRequest` | |
| DELETE | `/{id}` | CM, Admin | — | |
| GET | `/available-slots` | Vet, CM, Admin | — | Query params |
| POST | `/check-conflict` | Vet, CM, Admin | `ConflictCheckRequest` | Overlap check without saving |

## Quotations / Billing — `api/quotations`

| Method | Route | Roles | Body | Notes |
|---|---|---|---|---|
| GET | `/`, `/{id}` | Vet, CM, Admin | — | Org-scoped |
| POST | `/` | CM, Admin | `CreateQuotationRequest` | Totals recomputed server-side |
| PUT | `/{id}` | CM, Admin | `UpdateQuotationRequest` | Draft only — approved/finalized are read-only |
| POST | `/{id}/calculate` | CM, Admin | — | |
| POST | `/{id}/submit` | CM, Admin | — | Budget check; 409 illegal transition |
| POST | `/{id}/finalize` | CM, Admin | — | |

## Approvals — `api/approvals`

| Method | Route | Roles | Body | Notes |
|---|---|---|---|---|
| GET | `/pending`, `/{id}`, `/{id}/history` | Vet, CM, Admin | — | |
| POST | `/{id}/approve` | **CM only** | `ApproveRequest` | `ReviewedBy` bound server-side from JWT — client value ignored |
| POST | `/{id}/reject` | **CM only** | `RejectRequest` | Comment required |
| POST | `/{id}/revision` | **CM only** | `RequestRevisionRequest` | Comment required |

## Administration — `api/admin` — **Administrator only**

| Method | Route | Body → Response | Notes |
|---|---|---|---|
| GET | `/users` | — → `AdminUserResponse[]` | All platform users |
| PATCH | `/users/{id}/status` | `UpdateUserStatusRequest` → `AdminUserResponse` | Cannot deactivate self |
| **POST** | **`/users/veterinarians`** | `CreateStaffUserRequest` → `CreateStaffUserResponse` | Creates `Role=Veterinarian`; org must exist + be Active; `MustChangePassword=true`; one-time `temporaryPassword` in response; 400 dup email / inactive org, 404 unknown org |
| **POST** | **`/users/inventory-officers`** | `CreateStaffUserRequest` → `CreateStaffUserResponse` | Same, `Role=InventoryOfficer` |
| GET | `/organizations` | — → `AdminOrganizationResponse[]` | |
| PATCH | `/organizations/{id}/status` | `UpdateOrganizationStatusRequest` → `AdminOrganizationResponse` | Active/Rejected/Suspended/Inactive; reason required for Rejected/Suspended |
| GET | `/roles` | — → `string[]` | Role catalog |
| GET | `/system` | — → `AdminSystemInfoResponse` | Counts by role/status |

`CreateStaffUserRequest`: `{ firstName, lastName, email, phoneNumber?, organizationId }` — **no role field**; role is fixed by the endpoint. There is no generic `POST /api/admin/users` and no role-reassignment endpoint by design.

## Lookups — `api/lookups`

| Method | Route | Roles | Notes |
|---|---|---|---|
| GET | `/pets` | Vet, CM, Admin | Compact pet list for pickers |
| GET | `/medicines` | Vet, CM, IO, Admin | |

## AI-related endpoints

**None implemented.** The AI workflow screens are monitoring UI only — no backend AI/orchestration endpoints exist yet.
