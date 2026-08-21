# AI Usage Log — Scheduling, Billing & Approval Management

This log records every AI-assisted session used while designing and building the Scheduling, Billing and Approval features, in line with the SE3090 requirement to disclose AI assistance (date, tool/model, task/section, AI output, changes/rejections, verification).

## Entry 01

Date:
20 August 2026

AI Tool:
ChatGPT

Task / Section:
Scheduling, Billing & Approval domain and API design.

What the AI produced:
Suggestions for the domain entities, relationships,
business rules and REST API structure.

What I changed / rejected:
Kept the existing `Veterinarian` fields (Name, Specialisation,
Branch, Active) as-is since they already matched the frontend
type, and only added the missing CreatedAt/UpdatedAt audit
fields on top. Corrected the AI's initial `Appointment.Status`
suggestion (Scheduled/Confirmed/Completed/Cancelled) to instead
reuse the same 5-value AppointmentStatus enum already used by
AppointmentSlot in the frontend (Available/Reserved/Confirmed/
Completed/Cancelled), so frontend and backend stay consistent.
Rejected a generic/unscoped overlap-check description and
replaced it with the exact formula already implemented in
schedulingService.ts (`start < existingEnd && end > existingStart`)
so the business rule is traceable to real code. Added explicit
PK/FK types, CHECK constraints, indexes, and transaction
boundaries (slot booking, approval + history, quotation total
recalculation) that the AI's first draft did not include.
Added two endpoints (`POST /api/quotations/{id}/calculate`,
`GET /api/approvals/{id}/history`) beyond the AI's original
four-endpoint minimum to cover the calculate and audit-history
operations. Noted that `PetId` references the Pet entity owned
by another team member's module rather than defining it here.

How I verified the result:
Compared the design against the SE3090 Assignment 1
requirements and the PetCare project proposal.
Reviewed the relationships and business rules manually.
Cross-checked every entity/status/rule against the existing
frontend code (`frontend/web/src/types/domain.ts`,
`frontend/web/src/services/mockData.ts`,
`frontend/web/src/services/schedulingService.ts`,
`frontend/web/src/services/billingService.ts`) so the schema,
statuses, and business rules stay consistent with what the UI
already implements.

## Entry 02 — PostgreSQL / EF Core

**Date:** 20 August 2026

**AI Tool:** Devin IDE

**Task / Section:**
PostgreSQL database and Entity Framework Core implementation for Scheduling, Billing & Approval Management (domain entities, EF Core configurations, `PetCareDbContext`, migration, seed data).

**What the AI produced:**
Created the `PetCare.Domain` class library with entities (`Veterinarian`, `AppointmentSlot`, `Appointment`, `Quotation`, `QuotationItem`, `Approval`, `ApprovalHistory`), enums (`AppointmentStatus`, `AppointmentSlotStatus`, `QuotationStatus`, `ApprovalStatus`), and an `AuditableEntity` base class for `CreatedAt`/`UpdatedAt`. Created the `PetCare.Infrastructure` class library with an `IEntityTypeConfiguration<T>` per entity (PK/FK setup, `varchar` lengths, `numeric(12,2)`/`numeric(10,2)` precision, enum-to-string conversions, `CHECK` constraints for `EndTime > StartTime`, non-negative budget/subtotal/total/unit price, `Quantity > 0`, `TotalPrice = Quantity * UnitPrice`, and approval `ReviewedBy`/`Comment`-required-when-decided rules, plus unique/composite indexes). Wired all configurations into `PetCareDbContext` with a `SaveChanges` override that stamps audit timestamps server-side, added a design-time `IDesignTimeDbContextFactory` so migrations could be generated without `PetCare.Api` existing yet, generated the `InitialSchedulingBillingApproval` migration, and added `HasData` seed rows (3 veterinarians, 4 appointment slots) plus a separate `DevelopmentSeeder` for dev/test-only fixtures (2 appointments, 1 quotation with 2 line items, 1 pending approval) kept out of migration `HasData` so they never land in production.

**What I changed / rejected:**
Kept `QuotationItem.Category` as a plain `string` with a DB `CHECK` constraint instead of adding a fifth .NET enum, since the assignment's requested enum list only covered `AppointmentStatus`, `AppointmentSlotStatus`, `QuotationStatus`, `ApprovalStatus`. Used `DeleteBehavior.Restrict` on most FKs (only `Cascade` for `QuotationItem`→`Quotation` and `ApprovalHistory`→`Approval`) to avoid silently cascading deletes across the appointment/quotation/approval chain — the AI's first pass defaulted more relationships to `Cascade`. Rejected putting overlap/conflict detection, slot-bounds validation, and budget-vs-total submission gating into database `CHECK` constraints; kept those as application-layer validation since they require cross-row/query-time logic. Corrected the initial seed design so only reference data (`Veterinarian`, `AppointmentSlot`) uses EF Core `HasData` in the migration; moved the transactional dev/test rows (`Appointment`, `Quotation`, `QuotationItem`, `Approval`) into a separate `DevelopmentSeeder.SeedAsync` method, matching the domain model doc's instruction not to seed those in production data. Renamed the initially generated migration from `InitialScheduleBillingApproval` to the exact requested `InitialSchedulingBillingApproval` via `migrations remove` + re-add.

**How I verified it:**
Built the backend, created the `InitialSchedulingBillingApproval` migration, applied it to PostgreSQL, verified the migration was applied, and checked the tables and seed data in pgAdmin.

## Entry 03

**Date:** 20 August 2026

**AI Tool:** Devin IDE

**Task:**
Implement Scheduling business logic and tests.

**What the AI produced:**
Created the `PetCare.Application` class library (it did not exist yet — only an empty placeholder folder): `Interfaces/ISchedulingService.cs`, `Interfaces/IAppointmentRepository.cs`, `Interfaces/IAppointmentSlotRepository.cs`, `Interfaces/IVeterinarianRepository.cs`, `Interfaces/IUnitOfWork.cs`, `DTOs/Scheduling/*` (`CreateAppointmentRequest`, `UpdateAppointmentRequest`, `AppointmentResponse`, `AppointmentSlotResponse`, `ConflictCheckRequest`), `Exceptions/NotFoundException.cs` and `Exceptions/SchedulingConflictException.cs`, `Validators/AppointmentValidator.cs` (FluentValidation, structural/referential checks), and `Services/SchedulingService.cs` implementing the 8-step create-appointment flow (veterinarian exists → active → slot exists → slot belongs to veterinarian → requested time fits inside slot → no conflicting appointment → create → save) plus the overlap rule `newStart < existingEnd && newEnd > existingStart`. Implemented the repository/unit-of-work interfaces in `PetCare.Infrastructure/Repositories/` using EF Core against `PetCareDbContext`, keeping `PetCare.Application` free of any EF Core reference. Created the `PetCare.Application.Tests` xUnit project (none existed) with `Services/SchedulingServiceTests.cs` covering 10 scenarios: no-conflict success, same-vet/same-time conflict, new-starts-during-existing conflict, new-ends-during-existing conflict, new-surrounds-existing conflict, adjacent (back-to-back) allowed, different-veterinarian-same-time allowed, cancelled-appointment-no-conflict, inactive-veterinarian rejected, and outside-selected-slot rejected — all using mocked repositories/validators via Moq so the tests exercise only the Application layer.

**What I changed / rejected:**
Kept `PetId` existence validation out of `AppointmentValidator` (only checked for non-empty) since `Pet` is owned by another team member's module and no repository for it exists in this component. Rejected FluentValidation performing the deeper business checks (veterinarian active, slot belongs to veterinarian, appointment fits inside slot) — those stayed in `SchedulingService` since the service needs to re-load the same entities anyway to execute the use case, and duplicating that logic in the validator would create two sources of truth. Added one line to `SchedulingService.CheckConflictAsync` to explicitly filter out `Cancelled` appointments defensively at the Application layer, rather than relying solely on the Infrastructure repository's query filter — this was necessary to make the "cancelled does not conflict" rule actually testable and enforced at this layer rather than trusted-by-convention. Did not create `PetCare.Api`, controllers, or any HTTP endpoints, and did not touch Billing, Approval, Flutter, React, or Agentic AI code, per explicit scope for this session.

**How I verified it:**
Ran `dotnet build` on `PetCare.Domain`, `PetCare.Application`, and `PetCare.Infrastructure` (0 warnings/errors on all three), then ran `dotnet test` on `PetCare.Application.Tests`.

**Result:** 10 tests passed, 0 failed.

*Add a new `## Entry NN` block for each additional AI-assisted session.*

## Entry 04 — ASP.NET Core Scheduling API


**Date:**
21 August 2026


**AI Tool / Model:**
Devin IDE


**Task / Section:**
Implementation of the ASP.NET Core REST API for the Scheduling component.


**What the AI produced:**
Created the `PetCare.Api` ASP.NET Core 8 Web API project and wired the Scheduling module end-to-end. Added `Extensions/ServiceCollectionExtensions.cs` to register the application and infrastructure layers (repositories, `SchedulingService`, FluentValidation validators, `PetCareDbContext`). Configured `Program.cs` with `WebApplicationBuilder`, the CORS policy for `http://localhost:5173`, Swagger/OpenAPI with XML documentation comments, and a global `ExceptionHandlingMiddleware` that maps `NotFoundException` to 404, `SchedulingConflictException` to 409, `ValidationException` to 400, and unhandled errors to 500 using RFC7807 ProblemDetails. Implemented `Controllers/AppointmentsController.cs` with async endpoints for `GET /api/appointments`, `GET /api/appointments/{id}`, `POST /api/appointments`, `PUT /api/appointments/{id}`, `DELETE /api/appointments/{id}`, `GET /api/appointments/slots/available`, and `POST /api/appointments/conflicts`, all using `ISchedulingService` and returning the agreed status codes.

**What I changed / rejected:**
Kept the controller thin, delegating all business logic to `ISchedulingService` so the API layer only handles HTTP concerns. Confirmed the CORS allowed origin stays at `http://localhost:5173` to match the Vite React dev server. Did not add Billing or Approval controllers in this session because the assignment separates those into their own tasks. Kept Swagger enabled in Development only, which is the default generated setup.

**How I verified it:**
Ran `dotnet build backend/api/PetCare.sln` and confirmed all projects compiled with 0 warnings and 0 errors. Started the API with `dotnet run` and opened the Swagger UI at `http://localhost:5080/swagger` (the URL used in this session). Exercised the endpoints against the seeded PostgreSQL database.

**Issues encountered:**
`password authentication failed for user "postgres"` at runtime because `AddPetCareInfrastructure` had a hardcoded fallback connection string `Host=localhost;Port=5432;Database=petcare;Username=postgres;Password=postgres` that masked a missing real credential.

**How the issue was resolved:**
Removed the hardcoded PostgreSQL password fallback in `ServiceCollectionExtensions.cs` and made the connection-string resolution explicit: it now reads `ConnectionStrings:PetCareDb` from config (user secrets / appsettings), then `PETCARE_DB_CONNECTION` environment variable, and throws a clear `InvalidOperationException` if neither is set. Updated the comment in `appsettings.Development.json` to explain how to set the secret without committing it. Also fixed the empty-string handling so an empty `ConnectionStrings:PetCareDb` in `appsettings.Development.json` falls back to the environment variable instead of being treated as a valid connection string.

**Verification result:**
- `GET /api/appointments → 200 OK` returned the seeded appointments.
- `GET /api/appointments/slots/available?start=...&end=... → 200 OK` returned available slots.
- `POST /api/appointments → 201 Created` successfully created an appointment.
- `GET /api/appointments/{id} → 200 OK` returned the appointment by id.
- `PUT /api/appointments/{id} → 204 NoContent` updated the appointment.
- `DELETE /api/appointments/{id} → 204 NoContent` cancelled the appointment.
- `POST /api/appointments/conflicts → 200 OK` correctly reported a conflict for overlapping slots and no conflict for valid ranges.
- Build: 0 warnings, 0 errors.

## Entry 05 — Billing / Quotation Management (Application layer)

**Date:**
21 August 2026

**AI Tool / Model:**
Devin IDE

**Task / Section:**
Implement the Billing / Quotation Management use cases (Application + Infrastructure layers only; no controllers) for the Scheduling, Billing & Approval component: `IBillingService`/`BillingService`, Billing DTOs, `QuotationValidator`, `IQuotationRepository`/`QuotationRepository`, and xUnit tests for quotation calculation and validation.

**What the AI produced:**
Added `DTOs/Billing/*` (`CreateQuotationRequest`, `UpdateQuotationRequest`, `QuotationItemRequest`, `QuotationItemResponse`, `QuotationResponse` — the last including a computed `IsWithinBudget` flag). Added `Exceptions/BillingConflictException.cs` mirroring `SchedulingConflictException` for future 409 mapping. Added `Interfaces/IQuotationRepository.cs` and its EF Core implementation `Infrastructure/Repositories/QuotationRepository.cs` (loads `Quotation` with `Items` included, plus `ExistsForAppointmentAsync` to enforce the 1:1 Appointment↔Quotation rule). Added `Validators/QuotationValidator.cs` with `CreateQuotationRequestValidator`, `UpdateQuotationRequestValidator`, and a shared `QuotationItemRequestValidator` (Quantity > 0, UnitPrice >= 0, Budget >= 0, Description required, Category restricted to the same 5 values as the DB `CHECK` constraint, appointment-exists check, and the 1:1 quotation-per-appointment check). Added `Services/BillingService.cs` implementing: get all, get by id, create, update (full item-set replace), calculate (server-side recompute from persisted items), submit for approval (Draft/RevisionRequested → PendingApproval, blocked if Total > Budget), and finalize (Approved → Finalised). Registered everything in `ServiceCollectionExtensions.cs`. Added `tests/PetCare.Application.Tests/Services/BillingServiceTests.cs` (14 tests) and `tests/PetCare.Application.Tests/Validators/QuotationValidatorTests.cs` (18 tests).

**What I changed / rejected:**
Did not add a `TotalPrice`/`Subtotal`/`Total` field to any request DTO — `LineTotal`/`Subtotal`/`Total` are always recomputed server-side from `Quantity * UnitPrice`, so a malicious or buggy client can never override the calculated amount. Rejected letting `UpdateQuotationAsync` accept `AppointmentId` (immutable after creation) or apply to `Approved`/`Finalised` quotations — added an `EnsureEditable` guard that throws `BillingConflictException` for those statuses, matching the domain model's "approved quotations cannot be casually edited" rule. Rejected a soft/overridable budget check on submission; `SubmitQuotationForApprovalAsync` hard-blocks with `BillingConflictException` when `Total > Budget` since no override mechanism was specified. Did not create `QuotationsController` or touch `ExceptionHandlingMiddleware`, Scheduling code, the database schema, React, Flutter, or Agentic AI code, per the explicit scope for this session (existing `Quotation`/`QuotationItem` entities and EF configurations already supported everything needed).

**How I verified it:**
Ran `dotnet build backend/api/PetCare.sln` — build succeeded, 0 warnings, 0 errors. Ran `dotnet test backend/api/tests/PetCare.Application.Tests/PetCare.Application.Tests.csproj`.

**Result:** 42 tests passed, 0 failed (10 pre-existing Scheduling tests + 32 new Billing tests: 14 `BillingServiceTests` covering subtotal/total calculation, budget comparison, and status-transition rules; 18 `QuotationValidatorTests` covering item/budget/category/appointment-existence/duplicate-quotation validation).
