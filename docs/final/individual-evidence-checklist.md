# Individual Contribution Evidence Checklist

Use REAL git history only. `git log --author="Name" --oneline`.
76 commits total; authors on record: **Miran Ravisara (44), Thevin
Ranasinghe (25), Tharusha Cooray (12), Hiru Karunarathne (4)**.
Each member confirms their rows and fills [CONFIRM] items.

## Miran Ravisara — [component: merge/integration + docs lead, CONFIRM]

| Evidence | Reference |
|---|---|
| Merge_1 integration PRs #5–#11 | `0259a5b`, `75640ea`, `ad771ef`, `ebd0acb`, `0dce899`, `cf1491a` |
| Agentic agent integrations (diagnosis/consultation/scheduling/inventory) | `fcdbd1f`, `3358fa0`, `f8da4db`, `1988ef1` |
| Mobile app implementation | `49fb58d`, `d4ef4e3` |
| Security fix (Maps key externalization) | `1c0f0fe` |
| Docs finalization | `b84401c`, `7691d01` |
| AI usage log | `docs/ai/AI-Usage-Log-Member4.md` [CONFIRM which member] |

## Thevin Ranasinghe — [component, CONFIRM]

| Evidence | Reference |
|---|---|
| Scheduling/Billing/Approval domain + CI workflow | `backend-ci.yml`, commits in `git log --author=Thevin` |
| [CONFIRM from log — 25 commits] | run command above |

## Tharusha Cooray — [component, CONFIRM]

| Evidence | Reference |
|---|---|
| [CONFIRM from log — 12 commits] | `git log --author=Tharusha --oneline` |

## Hiru Karunarathne — [component, CONFIRM]

| Evidence | Reference |
|---|---|
| Diagnosis & treatment frontend workflow | `8876779` |
| [CONFIRM — 3 more commits] | `git log --author=Hiru --oneline` |

## Per-member template (fill manually)

- Owned components: [...]
- Key files: [...]
- Meaningful commits: [...]
- PRs authored/reviewed: [link or #]
- Tests written: [...]
- Agentic contribution (if any): [...]
- Documentation written: [...]
- Challenges & learning: [...]
- AI usage evidence: `docs/ai/AI-Usage-Log-Member[N].md`

## Uncommitted work attribution (Merge_2 working tree)

The orchestration work on `Merge_2` (supervisor graph, workflow tables,
E2E, dynamic-planning tests, deployment hardening) is **uncommitted** —
commit messages should credit actual contributors when committed.
