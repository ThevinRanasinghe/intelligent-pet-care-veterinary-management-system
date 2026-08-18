# Component boundary — Scheduling, Billing & Approval Management

Owned business scope on this branch:

- Veterinarian availability and appointment slots
- Conflict-free appointment slot validation
- Quotations and line-item billing
- Quote total and budget validation
- Manager approval/reject/revision workflow
- Approval validation summary UI
- AI workflow monitoring UI only

Not implemented here:

- Agent execution/orchestration
- LLM calls or model prompts
- ASP.NET Core persistence
- PostgreSQL migrations
- Flutter screens
- Other team members' business components

Integration expectations for later:

- React consumes the ASP.NET Core API only.
- Backend remains authoritative for business rules, authorization, persistence and transactions.
- AI is an internal service behind ASP.NET Core, not directly called by React/Flutter.
