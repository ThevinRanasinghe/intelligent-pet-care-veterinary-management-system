# ADR 0002 — Flutter State Management

**Status:** Accepted · **Date:** 2026 (implemented in `frontend/mobile`)

## Context

The Flutter client needs shared auth/session state and feature state (appointments, quotations, approvals) with testability.

## Options considered

- **`provider`** — official lightweight wrapper over InheritedWidget
- Riverpod — more powerful, larger API surface
- Bloc — heavier ceremony for this scope
- setState only — doesn't share auth state across screens

## Decision

`provider` (in `pubspec.yaml` — `provider: ^6.1.2`) with per-feature ChangeNotifiers (`AuthProvider`, `ApprovalProvider`, `BillingProvider`, scheduling providers) and `flutter_secure_storage` for the JWT.

## Consequences

- Familiar ChangeNotifier/Consumer pattern; easy to mock with `mockito` in `flutter_test`
- Providers map 1:1 to feature folders — no global store to coordinate
