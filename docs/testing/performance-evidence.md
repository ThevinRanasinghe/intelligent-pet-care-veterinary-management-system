# Performance Evidence - Agentic Workflow Stack (Local)

Measured with `tests/performance/perf-benchmark.ps1` against the local
disposable stack. Raw data: `tests/performance/results/latest.json`.

## Environment

| Item | Value |
|------|-------|
| OS | Windows 11 (10.0.26100) |
| CPU | AMD Ryzen 7 5700G |
| RAM | 27.9 GB |
| .NET SDK | 8.0.423 (ASP.NET Core 8, Development profile) |
| Python | 3.13.0 (`agentic-service/.venv`) |
| Database | postgres:16 in Docker (`petcare-e2e-pg`, localhost:55432) |
| LLM | `gemini-3.5-flash-lite` via Gemini API |
| PowerShell | 5.1 - concurrency via runspace pools |

## Method

- Each scenario issues N requests at concurrency C; per-request wall latency is
  measured with `Stopwatch` inside the worker and aggregated (avg, p50, p95,
  max, success/failure counts, wall time, throughput).
- Agentic scenarios run sequentially (N=3, C=1) to stay within Gemini quota.
- Unauthenticated baseline uses `GET /swagger/v1/swagger.json` (measured before
  the `/health` endpoint was added; `/health` now exists and returns DB
  reachability - it is the lighter always-on unauthenticated endpoint).
- DB-write and agentic scenarios use a fixture org/vet/owner/pet created over
  the API, plus 9 seeded `Available` slot rows (same mechanism as the E2E
  harness).

## Results (run 2026-10-05, all requests succeeded)

| Scenario | N | C | avg | p50 | p95 | max | throughput |
|----------|---|---|-----|-----|-----|-----|------------|
| swagger.json (unauth baseline) | 50 | 10 | 124.0 ms | 127.3 | 148.7 | 156.1 ms | 68.7 req/s |
| GET /consultations (manager) | 50 | 10 | 7.0 ms | 4.2 | 22.0 | 33.8 ms | 463.9 req/s |
| GET /appointments/available-slots | 50 | 10 | 8.0 ms | 4.3 | 24.5 | 33.9 ms | 496.6 req/s |
| POST /consultations create (owner) | 20 | 5 | 26.2 ms | 25.3 | 32.1 | 37.0 ms | 140.0 req/s |
| agentic workflow run (Gemini + graph) | 3 | 1 | 9,184 ms | 8,988 | 8,988 | 10,637 ms | - |
| agentic approve (resume + booking) | 3 | 1 | 323.3 ms | 338.8 | 338.8 | 359.8 ms | - |
| GET /consultations/{id}/analysis (agent) | 3 | 1 | 2,271.9 ms | 2,207 | 2,207 | 2,555 ms | - |

## Notes / limitations

- Single-node local machine; Postgres and both services share the host.
- Small N for agentic scenarios - LLM latency variance is high and dominated
  by Gemini round-trips, not the backend (~10 s per run is almost entirely
  model latency inside the supervisor + specialist agents).
- `swagger.json` is a heavier payload than a real health check; treat its
  numbers as an upper bound for unauthenticated cost.
- The approve path is fast because resume does no LLM call - deterministic
  validation + booking only.
- Numbers are indicative for local development, not production sizing.

