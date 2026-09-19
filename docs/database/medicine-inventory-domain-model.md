# Medicine & Inventory Domain Model

This document defines the production relational schema and operational behavior for the **Medicine and Inventory Management** component: entities, PK/FK relationships, constraints, indexes, PostgreSQL column types, atomic concurrency semantics, First-Expiry-First-Out (FEFO) dispensing mechanics, and audit transaction ledger.

---

## 1. Relational Entities & Schema

### Medicine (`Medicines`)
Core pharmaceutical master record holding catalog descriptions, unit pricing, reorder thresholds, and active physical/reserved quantity counters.

| Column | PostgreSQL Type | Nullable | Constraints / Defaults | Description |
|---|---|---|---|---|
| `Id` | `uuid` | No | PK, default `gen_random_uuid()` | Primary key |
| `Name` | `varchar(200)` | No | Index `IX_Medicines_Name` | Proprietary / commercial drug name |
| `Category` | `varchar(100)` | No | — | Clinical category (e.g. Antibiotics, NSAID) |
| `Description` | `text` | No | — | Indications, contraindications |
| `DosageForm` | `text` | No | — | Form (Tablet, Suspension, Injectable) |
| `Strength` | `text` | No | — | Active concentration (e.g. 250mg, 10mg/ml) |
| `UnitPrice` | `numeric(10,2)` | No | CHECK (`UnitPrice` >= 0) | Selling price per single unit (LKR) |
| `Manufacturer`| `varchar(200)` | No | — | Manufacturing lab or company |
| `Status` | `varchar(20)` | No | CHECK IN (`Active`, `Discontinued`) | Master record lifecycle |
| `ReorderLevel`| `integer` | No | CHECK (`ReorderLevel` >= 0) | Minimum stock threshold |
| `TotalQuantity`| `integer` | No | CHECK (`TotalQuantity` >= 0) | Total physical stock currently on hand |
| `ReservedQuantity`| `integer` | No | CHECK (`ReservedQuantity` >= 0) | Stock reserved for treatment holds |
| `CreatedAt` | `timestamptz` | No | — | Audit creation timestamp |
| `UpdatedAt` | `timestamptz` | No | — | Audit update timestamp |

**Check Constraints:**
- `CK_Medicine_Reserved_NotExceed_Total`: `CHECK ("ReservedQuantity" <= "TotalQuantity")`
- Available quantity is computed dynamically: `AvailableQuantity = TotalQuantity - ReservedQuantity`

---

### Supplier (`Suppliers`)
Certified pharmaceutical and equipment distribution vendors.

| Column | PostgreSQL Type | Nullable | Constraints / Defaults | Description |
|---|---|---|---|---|
| `Id` | `uuid` | No | PK, default `gen_random_uuid()` | Primary key |
| `Name` | `varchar(200)` | No | Index `IX_Suppliers_Name` | Registered supplier name |
| `ContactPerson` | `varchar(200)` | No | — | Primary contact point |
| `Phone` | `varchar(30)` | No | — | Telephone contact |
| `Email` | `varchar(200)` | No | — | Electronic mail address |
| `Address` | `varchar(500)` | No | — | Physical dispatch / business address |
| `Status` | `varchar(20)` | No | CHECK IN (`Active`, `Inactive`) | Status flag |
| `CreatedAt` | `timestamptz` | No | — | Audit creation timestamp |
| `UpdatedAt` | `timestamptz` | No | — | Audit update timestamp |

---

### MedicineBatch (`MedicineBatches`)
Physical inventory consignments / lots tracked by expiry date for FEFO dispensing.

| Column | PostgreSQL Type | Nullable | Constraints / Defaults | Description |
|---|---|---|---|---|
| `Id` | `uuid` | No | PK, default `gen_random_uuid()` | Primary key |
| `MedicineId` | `uuid` | No | FK -> `Medicines.Id` ON DELETE RESTRICT | Associated medicine |
| `SupplierId` | `uuid` | No | FK -> `Suppliers.Id` ON DELETE RESTRICT | Sourcing supplier |
| `BatchNumber` | `varchar(50)` | No | — | Manufacturer lot identifier |
| `Quantity` | `integer` | No | CHECK (`Quantity` >= 0) | Remaining physical units in this batch |
| `ExpiryDate` | `date` | No | Composite Index `(MedicineId, ExpiryDate)` | Manufacturer expiry date |
| `ReceivedDate`| `date` | No | — | Date consignment was booked in |
| `Status` | `varchar(20)` | No | CHECK IN (`Active`, `Depleted`, `Expired`) | Batch lifecycle status |
| `CreatedAt` | `timestamptz` | No | — | Audit creation timestamp |
| `UpdatedAt` | `timestamptz` | No | — | Audit update timestamp |

---

### MedicineReservation (`MedicineReservations`)
Temporary atomic holds on medicine stock initiated during consultations, diagnosis, or scheduled surgery.

| Column | PostgreSQL Type | Nullable | Constraints / Defaults | Description |
|---|---|---|---|---|
| `Id` | `uuid` | No | PK, default `gen_random_uuid()` | Primary key |
| `MedicineId` | `uuid` | No | FK -> `Medicines.Id` ON DELETE RESTRICT | Reserved drug |
| `Quantity` | `integer` | No | CHECK (`Quantity` > 0) | Units reserved |
| `Status` | `varchar(20)` | No | CHECK IN (`Reserved`, `Dispensed`, `Cancelled`, `Expired`) | Current reservation state |
| `ReferenceType`| `varchar(50)` | Yes | Composite Index `(ReferenceType, ReferenceId)` | Originating workflow (`Consultation`, `Treatment`) |
| `ReferenceId` | `uuid` | Yes | Composite Index `(ReferenceType, ReferenceId)` | Originating entity GUID |
| `RequestedByUserId` | `uuid` | Yes | — | User who requested the reservation |
| `CancelledByUserId` | `uuid` | Yes | — | User who cancelled the reservation |
| `CancelledAt` | `timestamptz` | Yes | — | Timestamp of cancellation |
| `CreatedAt` | `timestamptz` | No | — | Reservation timestamp |
| `UpdatedAt` | `timestamptz` | No | — | Modification timestamp |

---

### InventoryTransaction (`InventoryTransactions`)
Immutable audit log tracking all physical movements and reservation locks.

| Column | PostgreSQL Type | Nullable | Constraints / Defaults | Description |
|---|---|---|---|---|
| `Id` | `uuid` | No | PK, default `gen_random_uuid()` | Primary key |
| `MedicineId` | `uuid` | No | FK -> `Medicines.Id` ON DELETE RESTRICT | Target medicine |
| `BatchId` | `uuid` | Yes | FK -> `MedicineBatches.Id` ON DELETE RESTRICT | Affected batch (if physical stock movement) |
| `ReservationId`| `uuid` | Yes | FK -> `MedicineReservations.Id` ON DELETE RESTRICT | Associated reservation (if hold/release) |
| `Type` | `varchar(30)` | No | `StockIn`, `Reservation`, `ReservationRelease`, `Dispense`, `Disposal`, `Adjustment` | Transaction category |
| `QuantityChange`| `integer` | No | Signed value | Positive for stock-in/return, negative for dispense/loss |
| `PerformedByUserId` | `uuid` | No | — | Authenticated identity |
| `Notes` | `varchar(500)` | Yes | — | Clinical or audit rationale |
| `OccurredAt` | `timestamptz` | No | Index `IX_InventoryTransactions_OccurredAt` | UTC timestamp |

---

## 2. Core Business Workflows

### 2.1 Atomic Stock Reservation (Concurrency Control)
To prevent overselling and race conditions when multiple veterinarians concurrently prescribe medication:
```sql
UPDATE "Medicines"
SET "ReservedQuantity" = "ReservedQuantity" + @requestedQuantity,
    "UpdatedAt"        = @now
WHERE "Id" = @medicineId
  AND ("TotalQuantity" - "ReservedQuantity") >= @requestedQuantity;
```
- Returns number of affected rows.
- If `affected == 0`, the service immediately aborts and throws `InventoryConflictException`, mapped by middleware to **HTTP 409 Conflict**.
- If `affected == 1`, the reservation record is created and an audit transaction is logged.

### 2.2 First-Expiry-First-Out (FEFO) Dispensing
When dispensing a reservation:
1. Batches are fetched filtered by `Quantity > 0`, `Status == BatchStatus.Active`, and ordered by `ExpiryDate ASC`.
2. The algorithm iterates through batches, taking `Math.Min(remaining, batch.Quantity)` from each batch until the reserved quota is fulfilled.
3. Batch quantities are updated, physical `TotalQuantity` and `ReservedQuantity` on `Medicine` are decremented.
4. Each batch deduction creates an immutable `InventoryTransaction` record with `Type = Dispense`.
5. All updates commit atomically inside a single EF Core transaction (`SaveChangesAsync`).

### 2.3 Reservation Release / Cancellation
When a reservation is cancelled:
1. `ReleaseReservedAsync` atomically executes:
   ```sql
   UPDATE "Medicines"
   SET "ReservedQuantity" = GREATEST(0, "ReservedQuantity" - @quantity),
       "UpdatedAt" = @now
   WHERE "Id" = @medicineId;
   ```
2. Reservation status updates to `Cancelled`.
3. An audit transaction with `Type = ReservationRelease` is logged.

---

## 3. Role-Based Access Control (RBAC) Matrix

| Action | HTTP Endpoint | Allowed Roles |
|---|---|---|
| Search / List Medicines | `GET /api/medicines` | All authenticated users |
| View Medicine Details | `GET /api/medicines/{id}` | All authenticated users |
| View Batches (FEFO) | `GET /api/medicines/{id}/batches` | `ClinicManager`, `InventoryOfficer`, `SuperAdmin` |
| View Transaction Ledger | `GET /api/medicines/{id}/transactions` | `ClinicManager`, `InventoryOfficer`, `SuperAdmin` |
| Create Medicine | `POST /api/medicines` | `ClinicManager`, `InventoryOfficer`, `SuperAdmin` |
| Stock-In (Consignment) | `POST /api/medicines/{id}/stock-in` | `ClinicManager`, `InventoryOfficer`, `SuperAdmin` |
| Create Reservation | `POST /api/medicine-reservations` | `Veterinarian`, `ClinicManager`, `SuperAdmin` |
| Cancel Reservation | `POST /api/medicine-reservations/{id}/cancel` | `Veterinarian`, `ClinicManager`, `InventoryOfficer`, `SuperAdmin` |
| Dispense Reservation | `POST /api/medicine-reservations/{id}/dispense` | `ClinicManager`, `InventoryOfficer`, `SuperAdmin` |
| List Suppliers | `GET /api/suppliers` | `ClinicManager`, `InventoryOfficer`, `SuperAdmin` |
| Create Supplier | `POST /api/suppliers` | `ClinicManager`, `InventoryOfficer`, `SuperAdmin` |
