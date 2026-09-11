# Scheduling / Billing / Approval Domain Model

This document defines the normalized relational schema for the Scheduling, Billing and Approval modules: entities, PK/FK relationships, constraints, indexes, PostgreSQL column types, EF Core migration notes, seed data and transaction boundaries. The frontend UI types in `frontend/web/src/types/domain.ts` (`Veterinarian.name/specialisation/branch/active`) are consistent with the `Veterinarian` entity below; no backend persistence layer exists yet, so this is the target schema for the eventual EF Core / PostgreSQL implementation.

## Entities

### Veterinarian

| Field | PostgreSQL Type | Constraints |
|---|---|---|
| Id | `uuid` | PK, default `gen_random_uuid()` |
| Name | `varchar(150)` | NOT NULL |
| Specialisation | `varchar(150)` | NOT NULL |
| Branch | `varchar(100)` | NOT NULL |
| Active | `boolean` | NOT NULL, default `true` |
| CreatedAt | `timestamptz` | NOT NULL, default `now()` |
| UpdatedAt | `timestamptz` | NOT NULL, default `now()` |

### AppointmentSlot

| Field | PostgreSQL Type | Constraints |
|---|---|---|
| Id | `uuid` | PK, default `gen_random_uuid()` |
| VeterinarianId | `uuid` | NOT NULL, FK -> `Veterinarian.Id` |
| Date | `date` | NOT NULL |
| StartTime | `time` | NOT NULL |
| EndTime | `time` | NOT NULL, CHECK (`EndTime` > `StartTime`) |
| Branch | `varchar(100)` | NOT NULL |
| Status | `varchar(20)` | NOT NULL, CHECK IN (`Available`, `Reserved`, `Confirmed`, `Completed`, `Cancelled`), default `Available` |
| CreatedAt | `timestamptz` | NOT NULL, default `now()` |
| UpdatedAt | `timestamptz` | NOT NULL, default `now()` |

Constraint: `UNIQUE (VeterinarianId, Date, StartTime)` prevents double-booking the same vet at the same start time; overlap beyond exact match is enforced at the service/transaction layer (see [Transactions](#transactions)).

### Appointment

| Field | PostgreSQL Type | Constraints |
|---|---|---|
| Id | `uuid` | PK, default `gen_random_uuid()` |
| PetId | `uuid` | NOT NULL, FK -> `Pet.Id` (Pet entity implemented by the other component; see [Relationships](#relationships-erd-summary)) |
| VeterinarianId | `uuid` | NOT NULL, FK -> `Veterinarian.Id` |
| AppointmentSlotId | `uuid` | NOT NULL, UNIQUE, FK -> `AppointmentSlot.Id` |
| Date | `date` | NOT NULL |
| StartTime | `time` | NOT NULL |
| EndTime | `time` | NOT NULL, CHECK (`EndTime` > `StartTime`) |
| Status | `varchar(20)` | NOT NULL, CHECK IN (`Available`, `Reserved`, `Confirmed`, `Completed`, `Cancelled`), default `Reserved` |
| Notes | `text` | NULL |
| CreatedAt | `timestamptz` | NOT NULL, default `now()` |
| UpdatedAt | `timestamptz` | NOT NULL, default `now()` |

`AppointmentSlotId` is `UNIQUE` to enforce a 1—1 relationship: one confirmed appointment consumes exactly one slot.

### Quotation

| Field | PostgreSQL Type | Constraints |
|---|---|---|
| Id | `uuid` | PK, default `gen_random_uuid()` |
| AppointmentId | `uuid` | NOT NULL, UNIQUE, FK -> `Appointment.Id` |
| Budget | `numeric(12,2)` | NOT NULL, CHECK (`Budget` >= 0) |
| Subtotal | `numeric(12,2)` | NOT NULL, CHECK (`Subtotal` >= 0), default `0` |
| Total | `numeric(12,2)` | NOT NULL, CHECK (`Total` >= 0), default `0` |
| Status | `varchar(20)` | NOT NULL, CHECK IN (`Draft`, `PendingApproval`, `Approved`, `Rejected`, `RevisionRequested`, `Finalised`), default `Draft` |
| CreatedAt | `timestamptz` | NOT NULL, default `now()` |
| UpdatedAt | `timestamptz` | NOT NULL, default `now()` |

Derived rule: `Subtotal` = `SUM(QuotationItem.TotalPrice)`; `Total` = `Subtotal` (+ tax/fees if introduced later). Application/service layer should validate `Total <= Budget` before moving `Status` to `PendingApproval`.

### QuotationItem

| Field | PostgreSQL Type | Constraints |
|---|---|---|
| Id | `uuid` | PK, default `gen_random_uuid()` |
| QuotationId | `uuid` | NOT NULL, FK -> `Quotation.Id`, `ON DELETE CASCADE` |
| Category | `varchar(20)` | NOT NULL, CHECK IN (`Consultation`, `Examination`, `Treatment`, `Medicine`, `Other`) |
| Description | `varchar(500)` | NOT NULL |
| Quantity | `integer` | NOT NULL, CHECK (`Quantity` > 0) |
| UnitPrice | `numeric(10,2)` | NOT NULL, CHECK (`UnitPrice` >= 0) |
| TotalPrice | `numeric(12,2)` | NOT NULL, CHECK (`TotalPrice` = `Quantity` * `UnitPrice`) |

### Approval

| Field | PostgreSQL Type | Constraints |
|---|---|---|
| Id | `uuid` | PK, default `gen_random_uuid()` |
| QuotationId | `uuid` | NOT NULL, UNIQUE, FK -> `Quotation.Id` |
| Status | `varchar(20)` | NOT NULL, CHECK IN (`Pending`, `Approved`, `Rejected`, `RevisionRequested`), default `Pending` |
| ReviewedBy | `uuid` | NULL, FK -> `User.Id` (Clinic Manager) |
| ReviewedAt | `timestamptz` | NULL |
| Comment | `text` | NULL |

`QuotationId` is `UNIQUE`: one quotation has exactly one current approval record; status transitions are appended to `ApprovalHistory`.

### ApprovalHistory

| Field | PostgreSQL Type | Constraints |
|---|---|---|
| Id | `uuid` | PK, default `gen_random_uuid()` |
| ApprovalId | `uuid` | NOT NULL, FK -> `Approval.Id`, `ON DELETE CASCADE` |
| PreviousStatus | `varchar(20)` | NOT NULL |
| NewStatus | `varchar(20)` | NOT NULL |
| ChangedBy | `uuid` | NOT NULL, FK -> `User.Id` |
| Reason | `text` | NULL |
| ChangedAt | `timestamptz` | NOT NULL, default `now()` |

Audit trail: every write to `Approval.Status` must insert a corresponding `ApprovalHistory` row within the same transaction.

## Statuses

Status enums reuse the existing frontend terminology (`frontend/web/src/types/domain.ts`) so frontend and backend stay consistent.

### AppointmentSlot / Appointment status

| Status | Meaning |
|---|---|
| `Available` | Slot is open; not yet linked to an appointment |
| `Reserved` | Slot/appointment is held for a pending booking |
| `Confirmed` | Booking confirmed |
| `Completed` | Appointment has taken place |
| `Cancelled` | Booking cancelled |

Matches the frontend `AppointmentStatus` type. Applies to both `AppointmentSlot.Status` and `Appointment.Status`.

### Quotation status

| Status | Meaning |
|---|---|
| `Draft` | Quotation being prepared, not yet submitted |
| `PendingApproval` | Submitted, awaiting Clinic Manager decision |
| `Approved` | Approved by Clinic Manager |
| `Rejected` | Rejected by Clinic Manager |
| `RevisionRequested` | Manager requested changes before re-submission |
| `Finalised` | Approved and locked for execution/billing |

Matches the frontend `QuotationStatus` type.

### Approval status

| Status | Meaning |
|---|---|
| `Pending` | Awaiting manager review |
| `Approved` | Manager approved the quotation |
| `Rejected` | Manager rejected the quotation |
| `RevisionRequested` | Manager requested revisions |

Matches the frontend `ApprovalStatus` type. Each transition here is mirrored by an `ApprovalHistory` row (`PreviousStatus` -> `NewStatus`).

## Business rules

These are the business-specific operations (beyond CRUD) that the backend must enforce. They formalize logic already present in the frontend UI layer so behaviour stays identical when moved server-side.

### Scheduling

1. **No overlapping appointments for a veterinarian.** For the same `VeterinarianId` and `Date`, a new/updated appointment or slot must satisfy `NewStart < ExistingEnd AND NewEnd > ExistingStart` = `false` against every existing non-cancelled row. This formalizes the overlap check already implemented in `frontend/web/src/services/schedulingService.ts` (`hasVetConflict`: `start < existingEnd && end > existingStart`) and must be re-validated in the backend/database (e.g. inside the booking transaction) before commit.
2. **A slot must belong to an active veterinarian.** `AppointmentSlot.VeterinarianId` must reference a `Veterinarian` with `Active = true`; booking against an inactive vet is rejected.
3. **Appointment start must be before end.** `StartTime < EndTime` for both `AppointmentSlot` and `Appointment` (enforced via the `CHECK (EndTime > StartTime)` constraints defined above).
4. **Cancelled appointments do not block a slot.** Rows with `Status = 'Cancelled'` are excluded from the overlap check in rule 1, matching `hasVetConflict`'s `slot.status === 'Cancelled'` exclusion, so a cancelled booking frees the slot for reuse.
5. **An appointment must fit inside the selected availability slot.** `Appointment.StartTime >= AppointmentSlot.StartTime AND Appointment.EndTime <= AppointmentSlot.EndTime` for the linked `AppointmentSlotId`; an appointment cannot extend beyond the slot it was booked against.

### Billing

1. **Quantity must be greater than 0.** `QuotationItem.Quantity > 0` (`CHECK` constraint above).
2. **Unit price cannot be negative.** `QuotationItem.UnitPrice >= 0` (`CHECK` constraint above).
3. **TotalPrice = Quantity × UnitPrice.** Enforced via `CHECK (TotalPrice = Quantity * UnitPrice)`; matches the `quantity × unitPrice` calculation already done in `frontend/web/src/services/billingService.ts`.
4. **Quotation subtotal = sum of line totals.** `Quotation.Subtotal = SUM(QuotationItem.TotalPrice)` for all items on that quotation; recomputed transactionally on any item insert/update/delete (see [Transactions](#transactions)).
5. **Backend is authoritative for final quotation calculation.** The frontend calculation is UI-only preview; the backend must recompute `Subtotal`/`Total` server-side before persisting or approving a quotation, and reject any client-submitted total that doesn't match the recomputed value.
6. **Quotation total must be checked against the owner's budget.** `Quotation.Total` is compared to `Quotation.Budget` before a quotation can move to `PendingApproval`; exceeding budget should block submission or require explicit override/flagging.

### Approval

1. **Only `PendingApproval` quotations can be approved/rejected/revised.** An `Approval` action is only valid when the linked `Quotation.Status = 'PendingApproval'`; otherwise the action is rejected.
2. **Only an authorized Clinic Manager can make the decision.** `Approval.ReviewedBy` must reference a user with the Clinic Manager role; the API/service layer must authorize the caller before allowing a status change.
3. **Every approval action creates `ApprovalHistory`.** Any change to `Approval.Status` inserts a corresponding `ApprovalHistory` row (`PreviousStatus` -> `NewStatus`, `ChangedBy`, `ChangedAt`) in the same transaction — no silent status updates.
4. **Reject / RevisionRequested requires a reason.** When `Approval.Status` transitions to `Rejected` or `RevisionRequested`, `Comment` (on `Approval`) and `Reason` (on the corresponding `ApprovalHistory` row) must be non-empty.
5. **Approved quotations cannot be casually edited afterward.** Once `Quotation.Status = 'Approved'` (or `Finalised`), `QuotationItem` rows and `Quotation.Budget`/pricing fields become read-only; any change requires reopening via a new revision/approval cycle rather than a direct update.

## Relationships (ERD summary)

```
Veterinarian
    │
    ├── 1 : many ── AppointmentSlot
    │
    └── 1 : many ── Appointment


Pet
    │
    └── 1 : many ── Appointment


Appointment
    │
    └── 1 : 1 ── Quotation
                    │
                    └── 1 : many ── QuotationItem


Quotation
    │
    └── 1 : 1 ── Approval
                    │
                    └── 1 : many ── ApprovalHistory
```

- **Veterinarian 1—N AppointmentSlot**: a vet owns many slots (`AppointmentSlot.VeterinarianId` FK).
- **Veterinarian 1—N Appointment**: a vet performs many appointments (`Appointment.VeterinarianId` FK).
- **Pet 1—N Appointment**: a pet has many appointments (`Appointment.PetId` FK). `PetId` references the `Pet` entity that will be implemented by the other component (owner/pet management module) — not defined in this document.
- **Appointment 1—1 Quotation**: one appointment has exactly one quotation (`Quotation.AppointmentId` UNIQUE FK).
- **Quotation 1—N QuotationItem**: a quotation is composed of one or more line items (`ON DELETE CASCADE`).
- **Quotation 1—1 Approval**: one quotation has exactly one current approval record (`Approval.QuotationId` UNIQUE FK).
- **Approval 1—N ApprovalHistory**: every status change on an approval is recorded as an immutable history row (`ON DELETE CASCADE`).

`Appointment.AppointmentSlotId` is a separate UNIQUE FK to `AppointmentSlot` (recorded in the `Appointment` entity table above) used to mark which slot was consumed when the appointment was booked; it is not part of the primary ERD chain above but is enforced at the schema level.

## Indexes

- `IX_AppointmentSlot_VeterinarianId_Date` on `AppointmentSlot (VeterinarianId, Date)` — fast conflict/availability lookups.
- `IX_Appointment_PetId` on `Appointment (PetId)` — pet appointment history.
- `IX_Appointment_VeterinarianId_Date` on `Appointment (VeterinarianId, Date)`.
- `IX_QuotationItem_QuotationId` on `QuotationItem (QuotationId)`.
- `IX_Approval_Status` on `Approval (Status)` — pending-approval queue queries.
- `IX_ApprovalHistory_ApprovalId_ChangedAt` on `ApprovalHistory (ApprovalId, ChangedAt)`.

## EF Core migrations

- Model entities as EF Core entity classes with `Guid` PKs (`uuid` via Npgsql) and configure via `Fluent API` (`OnModelCreating`) rather than data annotations, to keep `CHECK` constraints and `numeric` precision explicit.
- Use `HasOne(...).WithOne(...).HasForeignKey(...)` for the 1—1 relationships (`AppointmentSlot`↔`Appointment`, `Appointment`↔`Quotation`, `Quotation`↔`Approval`) and enforce uniqueness with `HasIndex(...).IsUnique()`.
- Add `CHECK` constraints via `modelBuilder.Entity<T>().ToTable(t => t.HasCheckConstraint(...))` (EF Core 7+).
- Generate an initial migration (`dotnet ef migrations add InitialScheduleBillingApproval`) followed by incremental migrations per entity if introduced separately.
- `CreatedAt`/`UpdatedAt` should be set via `SaveChanges` interceptor or `DbContext` override so they are not client-settable.

## Seed data

- Seed a small set of `Veterinarian` rows (matching current mock data: e.g. Dr. Anika Perera / Small Animal Medicine / Colombo) via `HasData` in `OnModelCreating` or a dedicated seeding method, using fixed GUIDs for reproducibility across environments.
- Seed one or two sample `AppointmentSlot` rows in `Available` status per seeded vet for local development/testing.
- Do not seed `Appointment`, `Quotation`, `Approval` or `ApprovalHistory` rows in production seed data; only seed them in test/dev fixtures.

## Transactions

- **Booking a slot**: updating `AppointmentSlot.Status` to `Reserved`/`Confirmed` and inserting the `Appointment` row must happen in a single database transaction to avoid double-booking under concurrent requests; use `SERIALIZABLE` or row-level locking (`SELECT ... FOR UPDATE`) on the slot row, or rely on the `UNIQUE (VeterinarianId, Date, StartTime)` constraint plus retry-on-conflict.
- **Quotation approval**: transitioning `Approval.Status`, updating `Approval.ReviewedBy`/`ReviewedAt`, inserting the `ApprovalHistory` row, and updating `Quotation.Status` must all commit atomically within one transaction.
- **Quotation total recalculation**: any insert/update/delete of a `QuotationItem` should recompute and persist `Quotation.Subtotal`/`Total` in the same transaction as the item change.

## Notes

- This schema targets PostgreSQL with EF Core (Npgsql provider). The current frontend mock layer (`frontend/web/src/services/mockData.ts`, `frontend/web/src/types/domain.ts`) uses simplified string IDs and denormalised display fields (e.g. `veterinarianName` on slots); the backend implementation should expose normalized IDs and let the API/BFF layer join and denormalise for the UI.
- `Pet` and `User` referenced above (`PetId`, `ReviewedBy`, `ChangedBy`) are owned by other modules and are out of scope for this document beyond the FK reference.
