#intelligent-pet-care-veterinary-management-system

# PetCare AI

Integrated Pet Care & Veterinary Service Management System — SE3090 Assignment 1.

This repository is scaffolded as a shared monorepo so each team member can work from the same folder structure on separate GitHub branches.

## Current implementation scope

**Completed:** Scheduling, Billing & Approval Management in the React web application.

**Dashboard-only placeholders:** Pet & Consultation Requests, Diagnosis & Treatment, Medicine & Inventory, and other non-owned screens.

**AI:** UI only. No agentic AI orchestration, model calls, tool execution, or backend AI service is implemented yet. The interface is intentionally prepared for later integration.

## Architecture target

- `apps/web` — React staff/management application
- `api` — ASP.NET Core Web API project structure (implementation to be shared/merged by the team)
- `mobile` — Flutter application structure
- `ai-service` — future internal Agentic AI service
- `docs` — ADRs, database and API documentation
- `infra` — CI/CD and deployment configuration

The assignment requires React and Flutter to use the same ASP.NET Core API and PostgreSQL database, with protected routes and shared business rules.

## Run the web app

```bash
npm install
npm run dev
```

Then open `http://localhost:5173`.

The web app currently uses realistic local mock data. The service layer is separated so it can be switched to ASP.NET Core REST endpoints later without rewriting the UI.

## Key Scheduling/Billing/Approval workflows

1. View and filter veterinarian appointment slots.
2. Create a slot with basic business validation.
3. Detect obvious veterinarian conflicts before saving a slot.
4. Build and review quotations with automatic line-item totals.
5. Review AI-proposal approval cards and inspect the proposed appointment + quotation.
6. Approve, reject or request revision from a dedicated approval workflow.
7. View an AI workflow monitoring screen with planned steps and validation/approval states.

## Important integration note

The UI labels the AI section as **AI workflow interface only**. There are no agent implementations. This matches the requested development phase and keeps the branch merge-friendly.
# intelligent-pet-care-veterinary-management-system
