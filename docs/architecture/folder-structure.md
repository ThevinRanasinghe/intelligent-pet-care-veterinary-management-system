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
│   └── ai-service/              # future internal Agentic AI service
├── docs/
└── infra/
```

The React branch currently contains complete Scheduling, Billing and Approval UI plus AI workflow monitoring UI. Other feature folders are deliberately placeholders.
