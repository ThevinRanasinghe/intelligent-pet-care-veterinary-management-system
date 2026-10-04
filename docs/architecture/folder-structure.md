# Shared Repository Folder Structure

All four team branches should preserve this top-level layout.

```text
petcare-ai/
├── frontend/
│   ├── web/
│   │   └── src/
│   │       ├── components/       # shared React UI
│   │       ├── features/         # one folder per business component
│   │       ├── layouts/          # application shells
│   │       ├── pages/            # route-level dashboard pages
│   │       ├── routes/           # React Router definitions
│   │       ├── services/         # API boundaries and feature services
│   │       ├── store/            # shared state management boundary
│   │       ├── types/            # shared web types
│   │       ├── utils/            # format/validation helpers
│   │       └── tests/            # frontend tests
│   └── mobile/
│       ├── lib/
│       └── test/
├── backend/
│   ├── api/
│   │   ├── src/
│   │   │   ├── PetCare.Api/
│   │   │   ├── PetCare.Application/
│   │   │   ├── PetCare.Domain/
│   │   │   └── PetCare.Infrastructure/
│   │   └── tests/
├── agentic-service/             # internal Agentic AI service (FastAPI + LangGraph + Gemini, advisory-only)
│   ├── consultation_agent/
│   ├── diagnosis_agent/
│   ├── scheduling_agent/
│   ├── inventory_agent/
│   ├── shared/                  # backend client, config, LLM wiring
│   └── tests/
├── docs/
└── infra/
```

The React app implements all feature folders — `admin`, `auth`, `pets`, `consultations`, `scheduling`, `billing`, `approvals`, `treatment`, `inventory`, `vet`, `manager`, `dashboard`, `shared`. The advisory AI surfaces are embedded per-feature (consultation analysis + scheduling plan on the Consultation Requests page, "AI Assist" on examinations, "AI Plan" on medicine requests) rather than in a standalone `ai-workflows` folder, which was removed.
