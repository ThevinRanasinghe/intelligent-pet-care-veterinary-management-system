# React Testing — Scheduling, Billing & Approval Management

This document records the React/Vitest test suite for the Scheduling, Billing
and Approval Management component, in line with the SE3090 Step 10
requirement to add component tests, form-validation tests, API-integration
tests, and error-state tests using the existing React Testing Library +
Vitest setup.

## Test stack

- **Runner:** Vitest (jsdom environment)
- **Component testing:** React Testing Library (`@testing-library/react`,
  `@testing-library/user-event`, `@testing-library/jest-dom`)
- **API mocking:** Global `fetch` is stubbed per test (`vi.stubGlobal('fetch', ...)`)
  so tests exercise the real API boundary (`services/api.ts` → `apiRequest`)
  rather than mocking internal component functions. No real PostgreSQL
  database or running backend is required to run these tests.
- **Config:** `frontend/web/vite.config.ts` (`test` block: `environment: 'jsdom'`,
  `globals: true`, `setupFiles: ['./src/tests/setup.ts']`)

## How to run

```bash
cd frontend/web
npm install
npm run build      # tsc -b && vite build
npx vitest run      # or: npm run test:run
```

## Latest result

| Metric | Result |
| --- | --- |
| Test files | 9 |
| Tests | 48 |
| Passed | 48 |
| Failed | 0 |
| Build | Passed (`tsc -b && vite build` — 0 TypeScript errors) |

## Test files

| File | Focus |
| --- | --- |
| `src/tests/scheduling/SchedulingPage.test.tsx` | Component render, slot/appointment display, empty/loading/error states |
| `src/tests/scheduling/SchedulingPage.validation.test.tsx` | Required Pet ID, invalid start/end time, missing vet/slot data, successful booking |
| `src/tests/scheduling/schedulingService.api.test.ts` | GET available slots, POST appointment, POST check-conflict, DELETE cancel, success/409 handling |
| `src/tests/billing/BillingPage.test.tsx` | Component render, empty/loading/error states, backend-calculated total display |
| `src/tests/billing/BillingPage.validation.test.tsx` | Quantity/unit-price/budget/description validation, blocked vs. successful submission |
| `src/tests/billing/billingService.api.test.ts` | GET quotations, PUT quotation, POST calculate, POST submit, 400/409 handling |
| `src/tests/approvals/ApprovalPage.test.tsx` | Pending list render, empty/loading/error states, approval history render |
| `src/tests/approvals/ApprovalPage.actions.test.tsx` | Approve/reject/revision actions, reason enforcement, 409/400 error display |
| `src/tests/schedulingService.test.ts` (pre-existing) | `hasVetConflict` / `calculateQuoteTotal` pure-function tests |

## Coverage summary

- **Scheduling components** — page renders, slots and appointments listed,
  empty/loading/error states, Book/View/Edit/Cancel actions render correctly.
- **Scheduling validation** — Pet ID required, invalid start/end time
  rejected, missing veterinarian/slot data blocked, valid submission calls
  `createAppointment`.
- **Scheduling API integration** — `GET /appointments/available-slots`,
  `POST /appointments`, `POST /appointments/check-conflict`,
  `DELETE /appointments/{id}`, success and `ApiError` (409/400) handling.
- **Billing components** — quotation list renders, empty/loading/error
  states, backend-calculated total (not a client recalculation) displayed.
- **Billing validation** — quantity > 0, unit price ≥ 0, budget ≥ 0 (unit
  test on `validateQuotationInput`), required item fields, invalid
  submissions never call the API.
- **Billing API integration** — `GET /quotations`, `PUT /quotations/{id}`,
  `POST /quotations/{id}/calculate`, `POST /quotations/{id}/submit`,
  success and `ApiError` (400/409) handling.
- **Approval components** — pending approvals render, empty/loading/error
  states, approval history modal renders.
- **Approval actions** — approve calls `approve()`, reject/revision require
  a non-empty reason before the confirm action is enabled, successful
  decisions update the UI, backend 409/400 errors are displayed.
- **Loading/empty/error states** — covered for all three pages
  (Scheduling, Billing, Approval) against a mocked `fetch` boundary.

## Deferred (not failures)

These items are intentionally out of scope for this testing pass and are
**not** counted as failures — they are deferred to later stages:

- **Protected-route tests** — Deferred until authentication/protected
  routes are implemented. No auth/route-guard infrastructure exists yet in
  the frontend, so there is nothing to test.
- **Real PostgreSQL / end-to-end tests** — Deferred to the
  integration-testing stage. The unit/component suite mocks the `fetch`
  boundary intentionally so it can run without a live backend or database;
  real end-to-end verification against ASP.NET Core + PostgreSQL is tracked
  separately (see `docs/ai/AI-Usage-Log-Member4.md`, Entry 09, for the
  manual runtime verification already performed against the live stack).

## Related source changes made to support testing

- `frontend/web/src/utils/errors.ts` — shared `messageFrom()` helper so
  `SchedulingPage`, `BillingPage`, and `ApprovalPage` all surface backend
  `ApiError` detail/title messages instead of a generic `"API request
  failed: {status}"` string (a real bug fixed while writing error-state
  tests).
- `frontend/web/src/services/billingService.ts` — added
  `validateQuotationInput()` (quantity > 0, unit price ≥ 0, budget ≥ 0,
  required fields) and wired it into `BillingPage`'s save/submit actions so
  invalid quotations are blocked client-side before calling the API.
- `frontend/web/src/features/scheduling/SchedulingPage.tsx` — added a guard
  against booking a slot with missing veterinarian/slot data.

No backend business logic, PostgreSQL schema, Flutter code, or Agentic AI
code was modified as part of this testing work.
