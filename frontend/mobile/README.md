# petcare_mobile

Flutter Pet Owner companion app for Beacon Pet Health / PetCare. Mobile-first
UI built on the same Beacon black/yellow design system as the React web app —
not a shrunken desktop port. Staff (vet/manager/inventory) workflows stay on
the web app; this app covers the pet-owner journey end to end:

- Register / login (JWT, secure token storage)
- My Pets — create, edit, delete, photo or species-emoji avatar
- Consultation booking wizard — pet → clinic (map or list) → date → time →
  details, with fully-booked dates and booked slots disabled
- Appointments — Pending / Upcoming / History segments, detail page with a
  visual progress timeline
- Medical History — per-pet vertical timeline aggregating examinations,
  diagnoses, treatments and prescriptions
- Bills — owner invoices from `/quotations/mine` with payment status
- Profile — account details, edit profile, change password, logout

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

## API configuration

Base URL is compile-time configured — never hard-coded secrets:

```powershell
# Android emulator (10.0.2.2 = host machine's localhost)
flutter run --dart-define=API_BASE_URL=http://10.0.2.2:5019/api

# Flutter web / iOS simulator / desktop
flutter run -d chrome --web-port=5174 --dart-define=API_BASE_URL=http://localhost:5019/api

# Physical device — use the PC's LAN IP
flutter run --dart-define=API_BASE_URL=http://192.168.x.x:5019/api
```

`API_BASE_URL` is read in `lib/core/config/api_config.dart` (default
`http://10.0.2.2:5080/api`). Flutter talks only to the ASP.NET Core API;
Supabase credentials never live in this app. For Flutter web, the API's
dev CORS allowlist covers `localhost:5173`/`5174` — keep `--web-port=5174`.

Google Maps reads its key from local configuration — never committed.
Android: `GOOGLE_MAPS_API_KEY=<key>` in the gitignored `android/local.properties`
(see `local.properties.example`) injected via a manifest placeholder.
Web: `--dart-define=GOOGLE_MAPS_API_KEY=<key>`, injected into the page at
runtime — `tool/flutter_web.ps1` wraps `flutter run`/`build web` and feeds
the key from `local.properties` or the environment.

## Testing

```powershell
flutter analyze    # clean — 0 issues
flutter test       # 82 tests: unit, api-integration (FakeApiClient), widget
flutter build apk  # or: cd android; gradlew.bat assembleDebug "-Pkotlin.incremental=false"
```

Coverage includes the booking wizard (clinic list, fully-booked dates,
booked slots, required-field gating, 409 conflict message), pets CRUD,
quotation detail, bottom-nav order/tab switching, and the medical-history
timeline (loading/error/empty/selector + nested diagnosis→treatment→
prescription rendering).
