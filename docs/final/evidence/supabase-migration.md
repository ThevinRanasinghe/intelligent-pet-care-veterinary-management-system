# Supabase Migration Evidence — AddAgentWorkflows

Date: 2026-10-05 · Applied via `dotnet ef database update` (env `PETCARE_DB_CONNECTION`)

## Migration under review

`backend/api/src/PetCare.Infrastructure/Migrations/20261005135119_AddAgentWorkflows.cs`
— additive-only: creates 4 tables, 0 destructive operations. EF model configuration
(`AgentWorkflowConfiguration.cs`) matches: keys, FKs, indexes, nullability.

## `__EFMigrationsHistory` — after apply

All migrations present; `20261005135119_AddAgentWorkflows` was the only pending one.

## Tables created (verified via information_schema)

| Table | PK | Key constraints / indexes |
|---|---|---|
| `AgentWorkflows` | `Id` | UNIQUE `UX_AgentWorkflows_ConsultationRequestId`; `OrganizationId` FK → `SET NULL` |
| `AgentWorkflowSteps` | `Id` | FK → `AgentWorkflows` CASCADE; index on `WorkflowId` |
| `AgentWorkflowApprovals` | `Id` | FK → `AgentWorkflows` CASCADE; index on `WorkflowId` |
| `AgentWorkflowEvents` | `Id` | FK → `AgentWorkflows` CASCADE; index on `WorkflowId` |

## Post-migration verification

- API `/health` → `200 {"status":"Healthy","database":"reachable"}` (Supabase).
- Workflow create/retrieve/history/approve all exercised — see `live-smoke-test.md`
  (workflow `ba29be54` Completed, 33 events persisted to `AgentWorkflowEvents`,
  1 approval in `AgentWorkflowApprovals`, 6 step rows in `AgentWorkflowSteps`).
- Tenant isolation: workflow `OrganizationId` populated; foreign-org access
  returns 404 (verified in local E2E, same code path).

## Reproduce (any environment)

```powershell
$env:PETCARE_DB_CONNECTION = '<connection string>'   # secret — do not commit
dotnet ef database update `
  --project backend/api/src/PetCare.Infrastructure `
  --startup-project backend/api/src/PetCare.Infrastructure
```
