# API contract draft

This document is a UI-first contract. Routes can be finalized by the backend owner before merge.

## Scheduling

- `GET /api/scheduling/slots`
- `POST /api/scheduling/slots`
- `GET /api/scheduling/slots/{id}`
- `PATCH /api/scheduling/slots/{id}/status`
- `GET /api/scheduling/slots/conflicts`

Business operation: reject overlapping veterinarian/date/time slots.

## Billing

- `GET /api/quotations`
- `GET /api/quotations/{id}`
- `POST /api/quotations`
- `PUT /api/quotations/{id}`
- `POST /api/quotations/{id}/submit`

Business operation: calculate complete quotation and compare against the owner's budget.

## Approval

- `GET /api/approvals/pending`
- `GET /api/approvals/{id}`
- `POST /api/approvals/{id}/approve`
- `POST /api/approvals/{id}/reject`
- `POST /api/approvals/{id}/revision`

Business operation: high-impact execution stays blocked until an authorized Clinic Manager decision.

## AI UI boundary

- `GET /api/agent-workflows`
- `GET /api/agent-workflows/{id}`
- `GET /api/agent-workflows/{id}/execution-summary`

No AI execution endpoint is called by the current UI.
