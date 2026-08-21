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

## Entry 06 — Billing API Controller + Exception Middleware Fix

**Date:**
21 August 2026

**AI Tool / Model:**
Devin IDE

**Task / Section:**
Two related sessions on the Scheduling, Billing & Approval component's API layer: (1) expose the Entry 05 `IBillingService` application layer through ASP.NET Core, and (2) fix a validation-response serialization bug discovered while verifying (1).

**What the AI produced (session 1 — QuotationsController):**
Created `PetCare.Api/Controllers/QuotationsController.cs` injecting `IBillingService` and implementing `GET /api/quotations`, `GET /api/quotations/{id}`, `POST /api/quotations` (using `CreatedAtAction(nameof(GetQuotationById), ...)`), `PUT /api/quotations/{id}`, `POST /api/quotations/{id}/calculate`, `POST /api/quotations/{id}/submit`, and `POST /api/quotations/{id}/finalize`, all async and using the existing Billing DTOs with XML doc comments and `[ProducesResponseType]` attributes matching the Scheduling controller's style. Added a `BillingConflictException` case to `ExceptionHandlingMiddleware` mapping it to 409, alongside the existing `SchedulingConflictException` mapping.

**What I changed / rejected (session 1):**
Did not touch `ServiceCollectionExtensions.cs` since `IBillingService`/`BillingService` were already registered in Entry 05. Did not create a second API project, second exception middleware, or duplicate any `BillingService` logic — the controller is a thin HTTP wrapper only. Did not implement Approval endpoints, and did not modify `SchedulingService`, the database schema, React, or Flutter, per explicit scope.

**How I verified it (session 1):**
Ran `dotnet build backend/api/PetCare.sln` (0 warnings/errors). Started the API with `dotnet run` on `http://localhost:5080` and confirmed `swagger/v1/swagger.json` listed all 5 quotation route templates. Live-tested against PostgreSQL: `POST /api/quotations` created a quotation; a duplicate `POST` for the same appointment was correctly rejected (400, "quotation already exists"); `POST /api/quotations/{id}/finalize` on a `Draft` quotation correctly returned 409 via the new `BillingConflictException` mapping; `POST /api/quotations/{id}/calculate` and `POST /api/quotations/{id}/submit` both returned 200 with the quotation moving to `PendingApproval`.

**Issue encountered (challenge/learning — session 2):**
While verifying session 1, `POST /api/quotations` with an intentionally invalid body (negative budget, empty item fields, unknown category) returned `400 Bad Request` as expected, but the JSON body was just `{"title":"One or more validation errors occurred.","status":400,"instance":"/api/quotations"}` — **the field-level `errors` dictionary was silently missing**, even though `FluentValidation.ValidationException.Errors` clearly contained multiple entries (confirmed via the server console log). This made the 400 responses effectively useless to a frontend consumer, since no field could be highlighted as invalid. Root-caused it to `ExceptionHandlingMiddleware.HandleExceptionAsync`: `problemDetails` is assigned via a ternary between `new ProblemDetails{...}` and `new ValidationProblemDetails(errors){...}`; in C# the ternary's static type collapses to the common base type `ProblemDetails`, so `JsonSerializer.Serialize(problemDetails)` — which uses the compile-time generic type argument for its reflection metadata — serialized the object as a plain `ProblemDetails` and silently dropped the derived `Errors` property. This is a subtle C#/`System.Text.Json` gotcha (base-type erasure through a ternary) rather than a logic bug in the validators themselves, and it affected both Scheduling's and Billing's validation responses equally since it predated this session.

**How the issue was resolved:**
Changed the single serialization call to pass the object's runtime type explicitly: `JsonSerializer.Serialize(problemDetails, problemDetails.GetType())`. This forces `System.Text.Json` to reflect over the actual `ValidationProblemDetails` instance when that's what was constructed, restoring the `errors` property, while leaving every other line — including all exception-to-status-code mappings — untouched.

**What I changed / rejected (session 2):**
Rejected restructuring the `switch` expression or introducing a new response DTO, since the bug was purely in how the already-correct `problemDetails` object was serialized — a one-line fix was sufficient and lower-risk than refactoring the exception mapping logic. Did not modify `SchedulingService`, `BillingService`, the database schema, controllers' business logic, React, Flutter, or Agentic AI, per explicit scope for this session.

**Verification result (session 2):**
`dotnet build backend/api/PetCare.sln` → 0 warnings, 0 errors. Re-sent the same invalid `POST /api/quotations` request and confirmed the response now includes the full field-level errors:
```json
{"title":"One or more validation errors occurred.","status":400,"instance":"/api/quotations","errors":{"AppointmentId":["'Appointment Id' must not be empty.","Appointment does not exist."],"Budget":["Budget must be greater than or equal to 0."],"Items[0].Description":["Description is required."],"Items[0].Quantity":["Quantity must be greater than 0."],"Items[0].UnitPrice":["UnitPrice must be greater than or equal to 0."],"Items[0].Category":["Category must be one of: Consultation, Examination, Treatment, Medicine, Other."]}}
```
Re-confirmed the other mappings were undisturbed: `POST /api/quotations/{unknownId}/finalize` still returned 404 (`NotFoundException`), and the earlier `BillingConflictException → 409` case from session 1 was unaffected since no mapping logic changed, only the final serialization line.

## Entry 07 — Approval Management (Application layer + tests)

**Date:**
21 August 2026

**AI Tool / Model:**
Devin IDE

**Task / Section:**
Implement the Approval Management use cases (Application + Infrastructure layers only; explicitly no controllers) for the Scheduling, Billing & Approval component: `IApprovalService`/`ApprovalService`, Approval DTOs, request validators, `IApprovalRepository`/`ApprovalRepository`, and ApprovalHistory persistence, reusing the existing `PetCareDbContext`/`IUnitOfWork`/repository pattern from Entries 05–06.

**What the AI produced:**
Inspected `Approval.cs`, `ApprovalHistory.cs`, `Quotation.cs`, `ApprovalStatus`/`QuotationStatus` enums, `ApprovalConfiguration.cs`/`ApprovalHistoryConfiguration.cs` (including the `CK_Approval_ReviewedBy_Required_When_Decided` and `CK_Approval_Comment_Required_For_Reject_Or_Revision` check constraints), and the existing `BillingService`/`QuotationRepository` for pattern consistency, before writing any code. Added `DTOs/Approval/*` (`ApprovalResponse` with `QuotationTotal`/`QuotationBudget` context fields, `ApprovalHistoryResponse`, `ApproveRequest`, `RejectRequest`, `RequestRevisionRequest` — the latter two requiring a `Reason`). Added `Exceptions/ApprovalConflictException.cs` mirroring `BillingConflictException`. Added `Interfaces/IApprovalRepository.cs` and its EF Core implementation `Infrastructure/Repositories/ApprovalRepository.cs` (`GetByIdAsync` including `Quotation`+`History`, `GetByQuotationIdAsync`, `GetPendingAsync`, `AddAsync`, `AddHistoryAsync`, `GetHistoryAsync`). Added `Validators/ApprovalValidator.cs` (`ApproveRequestValidator` — `ReviewedBy` required only; `RejectRequestValidator`/`RequestRevisionRequestValidator` — `ReviewedBy` + non-empty `Reason` required). Added `Services/ApprovalService.cs` implementing: get pending approvals (with lazy Approval-row provisioning/reset, see below), get by id, approve, reject, request revision, and get approval history — each decision updates both `Approval.Status` and the linked `Quotation.Status` and appends an `ApprovalHistory` row in the same `SaveChangesAsync` call. Registered everything in `ServiceCollectionExtensions.cs`. Added `tests/PetCare.Application.Tests/Services/ApprovalServiceTests.cs` (17 tests).

**What I changed / rejected:**
Flagged and worked around a domain-model inconsistency instead of guessing silently: `BillingService.SubmitQuotationForApprovalAsync` (out of scope, not modified) transitions `Quotation.Status` to `PendingApproval` but never creates the corresponding 1:1 `Approval` row, so `GetPendingApprovalsAsync` would otherwise always return nothing. Rejected modifying `BillingService` to fix this (explicitly out of scope) and instead added `EnsureApprovalRecordsAreCurrentAsync` on the Approval side: it lazily creates a `Pending` `Approval` row for any `PendingApproval` quotation missing one, and resets an existing `Approval` back to `Pending` (with a `ChangedBy = Guid.Empty` "resubmitted for approval" history row) if a quotation cycles `RevisionRequested`/`Rejected` → edited → resubmitted → `PendingApproval` again. Rejected keying approve/reject/revision operations by `QuotationId` (would have conflated Billing's and Approval's identity spaces); kept them keyed by `ApprovalId` to match `docs/api/scheduling-billing-approval-api-contract.md`. Rejected silently trusting only `Approval.Status == Pending` before allowing a decision — added a second defensive check that `Approval.Quotation.Status == PendingApproval`, since the two are meant to stay in sync but are stored as independent columns. Did not implement JWT/role authorization: `ReviewedBy`/`ChangedBy` are accepted as caller-supplied `Guid` values on each request DTO, per the explicit instruction that the API security layer will add that later. Did not create `ApprovalsController`, modify `PetCareDbContext`/the database schema, create a second `DbContext` or a second Unit-of-Work implementation, or touch `SchedulingService`, `BillingService`'s business logic, React, Flutter, or Agentic AI code, per the explicit scope for this session.

**How I verified it:**
Ran `dotnet build backend/api/PetCare.sln` — build succeeded, 0 warnings, 0 errors. Ran `dotnet test backend/api/tests/PetCare.Application.Tests/PetCare.Application.Tests.csproj --logger "console;verbosity=normal"`.

**Result:** 59 tests passed, 0 failed (10 pre-existing Scheduling tests + 32 pre-existing Billing tests + 17 new `ApprovalServiceTests`: approve/reject/request-revision success paths, `ApproveRequestValidator`/`RejectRequestValidator`/`RequestRevisionRequestValidator` reason rules, rejecting an already-decided approval ("approved/rejected items cannot be reviewed again"), rejecting a decision when `Quotation.Status` isn't `PendingApproval`, `ApprovalHistory` row creation with correct `PreviousStatus`/`NewStatus`/`ChangedBy`/`Reason`, not-found handling on approve/reject/history/get-by-id, and lazy pending-approval provisioning). Build result: **0 warnings, 0 errors**. Test result: **17/17 new Approval tests passed, 59/59 total tests passed**.

## Entry 08 — Approval REST API + Swagger integration

**Date:**
21 August 2026

**AI Tool / Model:**
Devin IDE

**Task / Section:**
Expose the Entry 07 `IApprovalService` application layer as a REST API: create `ApprovalsController`, wire up any missing DI, return correct HTTP status codes, extend the exception middleware only for the new `ApprovalConflictException`, and add Swagger/XML docs — no business logic in the controller, no changes to Scheduling/Billing/schema/React/Flutter.

**What the AI produced:**
Inspected `AppointmentsController.cs` and `QuotationsController.cs` for the existing controller conventions (thin controllers delegating to an `I*Service`, `[ApiController]`/`[Route]`/`[Produces]`, `ProducesResponseType` attributes per status code, `Ok`/`NotFound` pattern for nullable Get-by-id results), `Program.cs` for the Swagger/middleware pipeline, and `ServiceCollectionExtensions.cs`, confirming `IApprovalService`/`IApprovalRepository` and the three approval validators were already registered from Entry 07 (no new DI needed). Created `Controllers/ApprovalsController.cs` with `GET /api/approvals/pending`, `GET /api/approvals/{id}`, `POST /api/approvals/{id}/approve`, `POST /api/approvals/{id}/reject`, `POST /api/approvals/{id}/revision`, `GET /api/approvals/{id}/history` — each an async one-line delegate to `IApprovalService`, matching the route shapes in `docs/api/scheduling-billing-approval-api-contract.md#approval`. Added `ApprovalConflictException -> 409` to `ExceptionHandlingMiddleware`'s exception-to-status `switch`, mirroring the existing `BillingConflictException`/`SchedulingConflictException` cases. Built the solution, started `PetCare.Api` on `http://localhost:5080` against the real Postgres `petcare` database, and ran the full API test workflow (A–G) plus a database-state check.

**What I changed / rejected:**
Rejected adding a `[FromBody]` reviewer-context wrapper or any new DTOs — reused `ApproveRequest`/`RejectRequest`/`RequestRevisionRequest`/`ApprovalResponse`/`ApprovalHistoryResponse` from Entry 07 as-is, since the task explicitly required reusing existing DTOs and not duplicating business logic in the controller. Rejected returning `201 Created` for approve/reject/revision (unlike `POST /api/quotations`) since these are decisions on an existing resource, not resource creation — used `Ok(...)` (`200`) instead, matching `QuotationsController.SubmitQuotation`/`FinalizeQuotation`'s pattern for status-transition endpoints. Rejected touching `Program.cs`'s Swagger setup — the existing `AddSwaggerGen`/`UseSwagger`/`UseSwaggerUI` calls auto-discover all controllers, including the new one, so no changes were needed there. Did not create any new DI registrations (all were already present from Entry 07). Did not modify `SchedulingService`, `BillingService`, `PetCareDbContext`/schema, React, Flutter, or Agentic AI code, per explicit scope.

**How I verified it:**
Ran `dotnet build backend/api/PetCare.sln` — 0 warnings, 0 errors. Started the API against the live Postgres database and fetched `swagger.json`, confirming all 6 `/api/approvals/*` paths were listed alongside the existing Scheduling/Billing paths. Drove 3 real quotations into `PendingApproval` via the existing Billing endpoints, then exercised the full workflow directly against the running API and Postgres-backed data (temporary curl payload `.json` files were created and deleted afterward; nothing was committed):
- **A.** `GET /api/approvals/pending` → `200`, returned all 3 pending approvals (confirming the Entry 07 lazy-provisioning logic works against real data).
- **B.** `GET /api/approvals/{id}` → `200` for a real id, `404` for `00000000-0000-0000-0000-000000000000`.
- **C.** `POST .../approve` on a Pending approval → `200`, `status: "Approved"`; linked `Quotation.Status` also became `Approved`.
- **D.** `POST .../reject` with `"reason": ""` → `400` with `{"errors":{"Reason":["Reason is required when rejecting a quotation."]}}`; retried with a real reason → `200`, `status: "Rejected"`.
- **E.** `POST .../revision` with `"reason": ""` → `400` with a `Reason`-required error; retried with a real reason → `200`, `status: "RevisionRequested"`.
- **F.** Re-`POST .../approve` on the now-`Approved` approval → `409`, `{"title":"The request conflicts with an existing approval business rule.","detail":"Only Pending approvals can be reviewed. Current status: 'Approved'."}`.
- **G.** `GET .../history` for all three approvals → `200`, one `ApprovalHistory` row each with the correct `PreviousStatus`/`NewStatus`/`ChangedBy`/`Reason`/`ChangedAt`.

**Database verification:** `psql`/Docker CLI were not available in this environment, so I verified persistence by re-querying the live API (which reads Postgres via EF Core on every call, with no caching layer): re-fetching `GET /api/quotations` after the decisions confirmed the three `Quotation.Status` values were durably `Rejected`/`RevisionRequested`/`Approved`, and re-fetching each approval's `/history` endpoint confirmed the `ApprovalHistory` rows were persisted (not just held in memory) with the correct decision data.

**Result:** Build: **0 warnings, 0 errors**. Swagger: all 6 approval endpoints listed correctly. API tests: **7/7 workflow steps (A–G) passed** with the expected status codes (`200`/`404`/`400`/`409`). PostgreSQL verification: **status transitions and ApprovalHistory rows confirmed persisted** via live re-reads.
