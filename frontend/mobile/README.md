# petcare_mobile — Flutter Pet Owner App

Flutter Pet Owner companion app for Beacon Pet Health / PetCare. Mobile-first
UI built on the same Beacon black/yellow design system as the React web app —
not a shrunken desktop port. Staff (vet/manager/inventory/admin) workflows stay
on the web app; this app covers the pet-owner journey end to end:

- Register / login (JWT, secure token storage)
- My Pets — create, edit, archive/restore, photo or species-emoji avatar
  (permanent delete only for pets with no clinical history)
- Consultation booking wizard — pet → clinic (map or list) → date → time →
  details, with fully-booked dates and booked slots disabled
- Appointments — Pending / Upcoming / History segments, detail page with a
  visual progress timeline
- Medical History — per-pet vertical timeline aggregating examinations,
  diagnoses, treatments and prescriptions
- Bills — owner invoices from `/quotations/mine` with payment status
- Profile — account details, edit profile, change password, logout
- AI workflow status — `ConsultationRequest.agentWorkflowStatus` (nullable,
  tolerant parsing) renders a friendly chip (`StatusBadge`) on the
  appointments list and consultation detail — e.g. `PendingManagerApproval`
  → "Clinic reviewing", `AwaitingExamination` → "Appointment confirmed";
  nothing renders when the field is absent (model-parsing + widget tests).

## Project structure

```
lib/
├── main.dart                  # PetCareApp, Provider wiring, 401→logout hookup
├── core/
│   ├── auth/                  # auth_service.dart (login/register/profile/password), token_storage.dart
│   ├── config/                # api_config.dart (API_BASE_URL dart-define)
│   ├── maps/                  # google_maps_loader.dart + _stub/_web conditional import (Flutter web Maps JS SDK injection)
│   ├── motion/                # app_motion.dart — FadeSlideIn/PressableScale animations
│   ├── network/               # api_client.dart (Bearer header, 401/403 handling)
│   ├── routing/               # app_router.dart
│   ├── state/                 # load_state.dart (shared LoadState enum)
│   ├── theme/                 # app_colors/app_text_styles/app_spacing/app_theme (Beacon tokens)
│   └── widgets/               # AppCard, StatusBadge, AppLoading/Empty/ErrorState, StepDots, StatusTimeline…
└── features/
    ├── auth/                  # login, register (POST /auth/register/pet-owner), staff_blocked_page
    ├── home/                  # main_shell (5 tabs), owner_home_tab, splash_page (session restore)
    ├── pets/                  # Active/Archived list, detail (edit/archive/restore), add/edit form
    ├── consultations/         # booking_wizard (pet→clinic→date→time→details), clinic_picker, detail
    ├── scheduling/            # owner_appointments (Pending/Upcoming/History), detail (directions)
    ├── billing/               # quotations list + read-only bill detail (/quotations/mine)
    ├── history/               # per-pet medical-history timeline
    ├── approval/              # legacy staff approvals pages — retained but unreachable in the owner app
    └── profile/               # details, edit profile, change password, logout
```

`test/` mirrors `lib/`: `test/api/` (FakeApiClient integration tests),
`test/unit/`, `test/widget/`, `test/navigation/`.

## Architecture & state management

- **Provider** pattern (`provider` package). One `ApiClient` is injected into
  feature services/providers; `AuthProvider` owns the session and drives
  top-level navigation.
- **UI states** use the shared `LoadState` enum + `AppLoading`/`AppErrorState`/
  `AppEmptyState` widgets — no per-page ad-hoc spinners.
- **Backend dependency:** the app is useless without a running PetCare API —
  every screen reads/writes through `ApiClient` (`core/network/api_client.dart`).
  There is no offline cache.

## Authentication & role behavior

- `POST /api/auth/login` returns a JWT; `token_storage.dart` persists it in
  `flutter_secure_storage`. `splash_page.dart` restores the session on cold
  start.
- `ApiClient` attaches `Authorization: Bearer <token>`; on **401** it clears
  the token and calls `onUnauthorized` → `AuthProvider.logout`. On **403** it
  returns an access-denied message without killing the session.
- **PetOwner-only gate:** any non-PetOwner role (Vet/Manager/IO/Admin) that
  signs in — or whose stored session restores — is routed to
  `staff_blocked_page.dart` with a "use the web app" notice and logout.
- Profile tab: edit profile (`PUT /auth/profile`), change password
  (`PUT /auth/change-password`), logout.

## Navigation

Five-tab bottom navigation (custom `AppBottomNav`, `PageView` with
`NeverScrollableScrollPhysics` — taps animate a 280ms directional slide and
keep-alive wrappers preserve tab state):

`Home · My Pets · Appointments · Bills · Profile`

No sidebar — deep pages (pet detail, medical history, quotation detail,
booking wizard) are pushed routes.

## Design system

Centralized tokens in `lib/core/theme/`:

- `app_colors.dart` — Beacon palette: `#FFC107` primary, `#111111` black,
  `#FFFBEF` cream background, `#6B6B6B` secondary text, `#E8E4D8` borders,
  plus success/warning/danger badge colors shared with the web CSS tokens.
- `app_text_styles.dart` — type scale (display 26, heading 20, section 18,
  body 15, caption 12).
- `app_spacing.dart` — `AppSpacing` 4–32 scale, 20 px page padding,
  `AppRadius` (cards 18, controls 13), `AppDurations`.
- `app_theme.dart` — Material 3 theme assembled from the tokens
  (scaffold = cream, cards = white with 18 px radius, inputs 13 px radius,
  yellow-on-black filled buttons, NavigationBar with yellow pill).

Reusable widgets in `lib/core/widgets/` — `AppCard`, `AppLoading` /
`AppEmptyState` / `AppErrorState`, `StatusBadge`, `SectionHeader`,
`DetailRow`, `PetCard`, `ListIconTile`, `StepDots` (wizard progress),
`StatusTimeline` (appointment progress), `FadeSlideIn` and `PressableScale`.

## Animations

Deliberately subtle — "animate changes, not everything":

- `FadeSlideIn` — card/list entrance (fade + 8 px rise, 250 ms, staggered)
- `PressableScale` — press feedback 1.0 → 0.97 → 1.0
- `StepDots` / `StatusTimeline` — step transitions at 200 ms
- No decorative/background animations; loading uses the shared states.

## Environment / build configuration

All endpoints and secrets are build-time injected — nothing sensitive is
committed. Flutter talks only to the ASP.NET Core API; Supabase credentials
never live in this app.

| Variable | Mechanism | Purpose |
|---|---|---|
| `API_BASE_URL` | `--dart-define` | PetCare API base incl. `/api` |
| `GOOGLE_MAPS_API_KEY` | `android/local.properties` or env var (Android); `--dart-define` (web) | Optional — enables the clinic map |

`API_BASE_URL` is read in `lib/core/config/api_config.dart` (compiled default
`http://10.0.2.2:5080/api` — always pass the define). For Flutter web, the
API's dev CORS allowlist covers `localhost:5173`/`5174` — keep
`--web-port=5174`.

- **Android:** `android/build.gradle.kts` reads `GOOGLE_MAPS_API_KEY` from
  gitignored `local.properties` (see `local.properties.example`) → env-var
  fallback → empty string, and feeds `AndroidManifest.xml`'s
  `${GOOGLE_MAPS_API_KEY}` placeholder. No key → the picker hides the map and
  shows the clinic list (booking still works).
- **Flutter web:** the Maps JS SDK is injected at runtime from
  `--dart-define=GOOGLE_MAPS_API_KEY` (`lib/core/maps/google_maps_loader_web.dart`,
  conditional import — native builds are unaffected). `tool/flutter_web.ps1`
  wraps `flutter run`/`build web` and feeds the key from `local.properties`/env.
- **AndroidManifest:** also declares `INTERNET` permission; do not add the key
  back to tracked files.

## Running

```powershell
flutter pub get

# Android emulator (10.0.2.2 = host machine's localhost)
flutter run --dart-define=API_BASE_URL=http://10.0.2.2:5019/api

# Physical device — PC's LAN IP; the API must bind beyond localhost
flutter run --dart-define=API_BASE_URL=http://192.168.x.x:5019/api

# Flutter web — wrapper script feeds the Maps key automatically
./tool/flutter_web.ps1 run     # or: flutter run -d chrome --web-port=5174 ...
```

For Flutter web keep `--web-port=5174` — the API's dev CORS allowlist covers
`localhost:5173`/`5174`.

## Building

```powershell
flutter build apk                    # release APK
flutter build apk --debug            # debug APK
# or via Gradle: cd android; ./gradlew.bat assembleDebug "-Pkotlin.incremental=false"
```

## Production release build

The deployed backend is `https://petcare-api-9hsw.onrender.com/api` (Render).
The app never talks to Supabase or the agentic service directly.

```powershell
flutter build apk --release `
  --dart-define=API_BASE_URL=https://petcare-api-9hsw.onrender.com/api
```

Output: `build/app/outputs/flutter-apk/app-release.apk`
Install on a device: `adb install app-release.apk`

- **API:** production value comes from the `API_BASE_URL` dart-define — no
  source change needed; the compiled default stays the emulator URL for dev.
- **Maps:** Android reads `GOOGLE_MAPS_API_KEY` from gitignored
  `android/local.properties` (see `local.properties.example`). Without a key
  the clinic picker falls back to its list view.
- **Signing:** release builds sign with a local keystore when gitignored
  `android/key.properties` exists (`storeFile`/`keyAlias`/`storePassword`/
  `keyPassword`), otherwise fall back to debug signing so CI/dev machines
  without the keystore still build. The keystore itself lives outside the
  repo (`%USERPROFILE%\petcare-release.jks`) and is never committed.
- **Version:** `pubspec.yaml` → `version: 1.0.0+1`.
- **Cold start:** the free-tier API sleeps after ~15 min idle — warm
  `https://petcare-api-9hsw.onrender.com/health` before demoing on device;
  a slow first request is expected infrastructure behavior, not an app bug.

## Testing

```powershell
flutter analyze    # clean — 0 issues
flutter test       # 116 tests: unit, api-integration (FakeApiClient), widget
```

Coverage includes the booking wizard (clinic list, fully-booked dates,
booked slots, required-field gating, 409 conflict message), pets CRUD +
archive/restore, auth/session restore, staff-blocking, quotation detail,
bottom-nav order/tab switching, and the medical-history timeline
(loading/error/empty/selector + nested diagnosis→treatment→prescription
rendering). `BookingWizardPage(useMap: false)` keeps Google Maps out of widget
tests. Historical per-step results are in `docs/testing/test-evidence-index.md`.

## Dependencies

`provider`, `http`, `flutter_secure_storage`, `google_maps_flutter`,
`url_launcher`, `web` (conditional Maps loader), plus `flutter_test` +
`mockito`/`build_runner` for tests. See `pubspec.yaml` for pinned versions.

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| Login/network errors on emulator | `API_BASE_URL` pointing at `localhost` — use `10.0.2.2` (emulator loopback) |
| Map area blank on Android | No key in `local.properties` — expected fallback; add `GOOGLE_MAPS_API_KEY` |
| Map blank on Flutter web | Missing `--dart-define=GOOGLE_MAPS_API_KEY` — use `tool/flutter_web.ps1` |
| 403 after staff login | By design — the app is PetOwner-only; use the React app |
| API not reachable from a phone | Use the PC's LAN IP and make the API bind beyond localhost (`--urls`) |
| CORS error on Flutter web | Keep `--web-port=5174` (dev allowlist), or extend `Cors:AllowedOrigins` |
