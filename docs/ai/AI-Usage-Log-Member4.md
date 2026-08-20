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

## Entry 02

Date:
[dd Month yyyy]

AI Tool:
[Tool / model name]

Task / Section:
[e.g. ER diagram generation, endpoint list finalisation]

What the AI produced:
[Describe the output]

What I changed / rejected:
[Write what I actually changed or rejected.]

How I verified the result:
[Describe verification steps]

*Add a new `## Entry NN` block for each additional AI-assisted session.*
