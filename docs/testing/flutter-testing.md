# Flutter Testing — Scheduling, Billing & Approval

This document records the Flutter test suite for the Scheduling, Billing and
Approval Management mobile client, in line with the SE3090 Step 12 requirement
to implement and verify the Flutter mobile contribution for the owned
component. It records only results that were actually observed by running the
commands; no result is inferred from the implementation.

## Environment

| Tool | Version |
| --- | --- |
| Flutter | 3.47.2 (channel stable) |
| Dart | 3.13.2 |
| Android SDK | 36.0.0 |
| OS | Windows 11 Pro 64-bit, 24H2 |

Flutter was invoked from `C:\Flutter\flutter\bin`. The Android toolchain is
configured against `D:\Software\Android`. Visual Studio is not installed; this
only affects Windows desktop builds and is not required for Android APK
generation.

## How to run

```bash
cd frontend/mobile
flutter pub get
flutter analyze
flutter test
flutter build apk --debug
```

For a physical device, override the API base URL at runtime:

```bash
flutter run --dart-define=API_BASE_URL=http://<your-pc-ip>:5080/api
```

For the Android emulator, the default `http://10.0.2.2:5080/api` in
`lib/core/config/api_config.dart` is used.

## Static Analysis

- **Command:** `flutter analyze`
- **Result:** Passed — No issues found (exit 0)

## Automated Tests

- **Command:** `flutter test`
- **Test files:** 8
- **Tests:** 49
- **Passed:** 49
- **Failed:** 0

### Test files

| File | Focus | Tests |
| --- | --- | ---: |
| `test/api/api_integration_test.dart` | Scheduling/Billing/Approval service API parsing and 401/403/500 error handling against a fake `ApiClient` | 10 |
| `test/api/auth_session_test.dart` | Login/logout/session restoration, exact `Authorization: Bearer <token>` header, 401 token/storage/provider cleanup, 403 session preservation, login-logout-login again | 9 |
| `test/unit/model_parsing_test.dart` | Appointment/Quotation/QuotationItem/Approval/ApprovalHistory JSON parsing, nullable fields, numeric coercion | 9 |
| `test/navigation/navigation_test.dart` | Slot tap navigates to appointment detail using the real appointment ID (not the slot ID); unbooked slot shows an empty state | 2 |
| `test/widget/appointment_slots_page_test.dart` | Loading/empty/error states, retry recovery, pull-to-refresh | 4 |
| `test/widget/quotations_page_test.dart` | Loading/empty/error states for the quotations list | 3 |
| `test/widget/approvals_page_test.dart` | Pending approvals loading/empty/error; approval history render/empty; tapping an approval opens ApprovalDetailPage with the real approval ID | 6 |
| `test/widget/approval_detail_page_test.dart` | ApprovalDetailPage loading/success/404-error/500-error/retry; View History navigates to ApprovalHistoryPage | 6 |

No existing tests were deleted. New tests were added to cover regressions
discovered during this verification pass.

## APK Build

- **Command:** `flutter build apk --debug`
- **Result:** Passed (exit 0)
- **APK:** `frontend/mobile/build/app/outputs/flutter-apk/app-debug.apk`

Gradle emitted a Java native-access warning (`java.lang.System::load`); this
is a JDK 24 warning and does not affect the build result.

## Runtime Verification

- **Backend:** `http://localhost:5080` — started with `dotnet run
  --no-launch-profile --urls http://localhost:5080` in the Development
  environment. No automatic seeding was invoked.
- **Host-side API checks (PowerShell, not Flutter on Android):**
  - `POST /api/auth/login` (valid) → 200, JWT received
  - `POST /api/auth/login` (invalid) → 401
  - `GET /api/appointments/available-slots` → 200, 3 slots
  - `GET /api/appointments` → 200, 3 appointments; each detail → 200
  - `GET /api/quotations` → 200, 3 quotations; each detail → 200
  - `GET /api/approvals/pending` → 200, 1 approval
  - `GET /api/approvals/{id}` → 200; `GET /api/approvals/{id}/history` → 200
  - `GET /api/appointments/available-slots?date=1900-01-01` → 200, empty array
- **Android runtime:** Pending — no emulator/device available. The following
  remain unverified on a real Android runtime:
  - Emulator-to-host connectivity to `http://10.0.2.2:5080/api`
  - Real device secure-storage restoration
  - Live UI loading/empty/error states
  - Real-role 403 enforcement with a non-manager account

## Bugs Fixed During Verification

- **401 session cleanup:** `ApiClient` now clears its in-memory token on 401
  and awaits the `onUnauthorized` callback; `PetCareApp` wires that callback to
  `AuthProvider.logout` and resets its navigator on authentication changes.
  Previously a 401 cleared storage but left the provider signed in.
- **403 handling:** `ApiClient` now returns an explicit access-denied message
  for 403 and does not clear the token. Previously 403 produced a generic
  status-only message.
- **Slot navigation:** Tapping a slot previously requested
  `/api/appointments/{slotId}`, using a slot ID as an appointment ID. It now
  resolves the associated non-cancelled appointment through
  `GET /api/appointments` and fetches detail using the real appointment ID.
  An unbooked slot displays "No appointment has been booked for this slot".
- Removed competing login/logout navigation in `LoginPage`/`HomePage` and the
  duplicate startup loading in `HomePage`; navigation is now driven by
  `AuthProvider` state through `PetCareApp`.

## Known Limitations

- Android runtime UI verification is pending (no emulator/device available).
- Real non-manager 403 verification is pending.
- Some display fields remain IDs (veterinarian, pet, appointment, quotation,
  reviewer) because the current API DTOs do not provide names. No missing
  pet/owner/veterinarian/branch data was fabricated.

## Database Scope Correction

During this verification pass, the existing `AddUsers` migration was applied
and a development user was inserted despite the instruction not to modify the
PostgreSQL schema. Automatic startup seeding was then added to `Program.cs`
and failed on an `IX_Appointments_AppointmentSlotId` uniqueness conflict.

Recovery actions taken:
- Removed the newly added startup-seeding code from `Program.cs`; the file
  now has no remaining diff.
- No further automatic seeding is performed.
- No database rollback or deletion was attempted.

The applied `AddUsers` migration and the inserted development user remain in
the database. Whether that migration belongs in the shared project is to be
decided with the team. No new migration was created to hide the change.

## Related Source Changes

- `frontend/mobile/lib/features/approval/approval_detail_page.dart` — new
  dedicated ApprovalDetailPage with loading/success/404-error/500-error/retry
  states and a View History button navigating to ApprovalHistoryPage.
- `frontend/mobile/lib/features/approval/approval_provider.dart` — added
  `detailState`, `approval`, and `loadApproval(id)` matching the
  BillingProvider detail pattern.
- `frontend/mobile/lib/features/approval/approvals_page.dart` — tapping a
  pending approval now opens ApprovalDetailPage using the real approval ID
  instead of jumping directly to history.
- `frontend/mobile/lib/core/routing/app_router.dart` — added `/approval` route
  → ApprovalDetailPage.
- `frontend/mobile/test/widget/approval_detail_page_test.dart` — new tests
  for loading, success, 404, 500, retry, and View History navigation.
- `frontend/mobile/test/widget/approvals_page_test.dart` — added test
  verifying tapping an approval opens ApprovalDetailPage with the real
  approval ID.
- `frontend/mobile/lib/core/network/api_client.dart` — 401 token cleanup,
  403 access-denied message, async `onUnauthorized`.
- `frontend/mobile/lib/core/routing/app_router.dart` — added
  `/slot-appointment` route for slot-to-appointment resolution.
- `frontend/mobile/lib/main.dart` — wired `onUnauthorized` to
  `AuthProvider.logout`; navigator resets on authentication changes.
- `frontend/mobile/lib/features/auth/login_page.dart` — removed competing
  post-frame navigation.
- `frontend/mobile/lib/features/home/home_page.dart` — removed duplicate
  startup loading and logout navigation.
- `frontend/mobile/lib/features/scheduling/appointment_slots_page.dart` —
  slot tap now uses `/slot-appointment`.
- `frontend/mobile/lib/features/scheduling/appointment_detail_page.dart` —
  added `forSlot` mode and unbooked-slot empty state.
- `frontend/mobile/lib/features/scheduling/scheduling_provider.dart` —
  `loadAppointment` supports `forSlot`.
- `frontend/mobile/lib/features/scheduling/scheduling_service.dart` — added
  `getAppointmentForSlot`.
- `frontend/mobile/test/api/auth_session_test.dart` — new auth/session
  regression tests.
- `frontend/mobile/test/helpers/fake_api_client.dart` — records requested
  paths for navigation assertions.
- `frontend/mobile/test/navigation/navigation_test.dart` — slot-to-appointment
  ID assertion and unbooked-slot test.
- `frontend/mobile/test/widget/appointment_slots_page_test.dart` — retry and
  pull-to-refresh test.
- `backend/api/src/PetCare.Api/Program.cs` — restored to pre-seeding
  contents; no remaining diff.

No backend business logic, PostgreSQL schema (intentionally), React code,
or Agentic AI code was modified as part of this verification work.
