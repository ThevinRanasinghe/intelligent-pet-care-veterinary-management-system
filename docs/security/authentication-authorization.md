# Authentication & Authorization

This document describes the actual security implementation for the Scheduling, Billing & Approval Management component. All claims below are verified against the source code in `PetCare.Api`, `PetCare.Application`, `PetCare.Infrastructure/Security`, `frontend/web/src/features/auth`, and `frontend/mobile/lib/core/auth`.

---

## 1. Authentication overview

The system uses JWT bearer token authentication. A user logs in with an email and password; the backend verifies the password against a PBKDF2 hash, issues a signed JWT (HMAC-SHA256) carrying the user's ID and role, and returns the token plus profile information. Both the React web client and the Flutter mobile client store the token and attach it as a `Bearer` token in the `Authorization` header on subsequent API requests.

---

## 2. Login flow

### Endpoint

```
POST /api/auth/login
Content-Type: application/json
```

### Request body

```json
{
  "email": "manager@petcare.lk",
  "password": "ChangeMe123!"
}
```

### Flow (verified in `AuthService.LoginAsync`)

1. **Structural validation** — `LoginRequestValidator` (FluentValidation) checks the email is non-empty and well-formed, and the password is non-empty. Failure → 400 with field-level errors.
2. **User lookup** — `IUserRepository.GetByEmailAsync` fetches the user by email.
3. **Credential verification** — If the user is not found, is not active (`Active = false`), or the password does not match (`IPasswordHasher.VerifyPassword` returns false), an `InvalidCredentialsException` is thrown → 401. The same error is returned for all three cases to avoid user enumeration.
4. **JWT generation** — `IJwtTokenGenerator.GenerateToken` creates and signs the token.
5. **Response** — Returns `LoginResponse` (200) with `Token`, `ExpiresAt`, `UserId`, `Email`, `Name`, `Role`. The password and password hash are never returned.

### Error responses

| Scenario | Status | Body |
|---|---|---|
| Malformed/empty email or password | 400 | Validation problem details with field errors |
| Unknown email / wrong password / inactive account | 401 | `{"title":"Invalid email or password."}` |

---

## 3. JWT

### Generation (verified in `JwtTokenGenerator.cs`)

- **Algorithm:** HMAC-SHA256 (`SecurityAlgorithms.HmacSha256`).
- **Signing key:** `SymmetricSecurityKey` created from the UTF-8 bytes of the configured key.
- **Claims issued** (confirmed in `JwtTokenGenerator.GenerateToken`):

| Claim | Value |
|---|---|
| `JwtRegisteredClaimNames.Sub` | `user.Id` (Guid) |
| `ClaimTypes.NameIdentifier` | `user.Id` (Guid) |
| `ClaimTypes.Email` | `user.Email` |
| `ClaimTypes.Name` | `user.Name` |
| `ClaimTypes.Role` | `user.Role` (e.g. `ClinicManager`) |
| `JwtRegisteredClaimNames.Jti` | Random `Guid` (token uniqueness) |

- **Expiration:** `DateTimeOffset.UtcNow.AddMinutes(ExpiryMinutes)`. Default `ExpiryMinutes = 60` (from `JwtOptions` and `appsettings.json`).

### Validation (verified in `ServiceCollectionExtensions.AddPetCareAuthentication`)

The API validates:
- **Issuer** — `ValidateIssuer = true`, `ValidIssuer` from config (default `PetCareApi`).
- **Audience** — `ValidateAudience = true`, `ValidAudience` from config (default `PetCareClient`).
- **Signing key** — `ValidateIssuerSigningKey = true`.
- **Lifetime** — `ValidateLifetime = true`, `ClockSkew = 30 seconds`.

---

## 4. Configuration

### JWT key resolution (verified in `ServiceCollectionExtensions.cs`)

The current implementation prefers a non-whitespace `Jwt:Key` configuration value and falls back to the `PETCARE_JWT_KEY` environment variable:

```csharp
var key = !string.IsNullOrWhiteSpace(jwtSection["Key"])
    ? jwtSection["Key"]
    : Environment.GetEnvironmentVariable("PETCARE_JWT_KEY");
```

If neither is set, the API throws `InvalidOperationException` at startup and refuses to start. No hardcoded fallback key exists.

### Configuration sources

| Setting | Source | Default |
|---|---|---|
| `Jwt:Issuer` | `appsettings.json` / `appsettings.Development.json` | `PetCareApi` |
| `Jwt:Audience` | `appsettings.json` / `appsettings.Development.json` | `PetCareClient` |
| `Jwt:ExpiryMinutes` | `appsettings.json` / `appsettings.Development.json` | `60` |
| `Jwt:Key` | User secrets (`dotnet user-secrets set "Jwt:Key" "..."`) or `PETCARE_JWT_KEY` env var | _(none — required)_ |
| `ConnectionStrings:PetCareDb` | User secrets or `PETCARE_DB_CONNECTION` env var | _(none — required)_ |

No actual secret values appear in any committed file. `appsettings.json` and `appsettings.Development.json` contain empty `Key` values with comments instructing developers to use user secrets or the environment variable.

---

## 5. Authorization

### Role names (confirmed in `PetCare.Domain/Constants/Roles.cs`)

| Constant | Value | Description |
|---|---|---|
| `Roles.PetOwner` | `"PetOwner"` | Owns pets and consultation requests; reads own clinical history |
| `Roles.Veterinarian` | `"Veterinarian"` | Clinical write operations; reads scheduling/billing |
| `Roles.InventoryOfficer` | `"InventoryOfficer"` | Medicines, suppliers, stock, reservations, dispensing |
| `Roles.ClinicManager` | `"ClinicManager"` | Scheduling, quotations/billing, approval decisions, read-only clinic oversight |
| `Roles.SuperAdmin` | `"Administrator"` | Platform administration (users, organizations, system); cross-organization visibility |
| `Roles.Staff` | `"Staff"` | General authenticated staff member (defined but currently unused) |

All authorization attributes reference the `Roles` constants — no role strings are hard-coded.

### Endpoint / role matrix (verified against controller attributes)

| Endpoint group | Read | Mutate |
|---|---|---|
| `POST /api/auth/login`, `POST /api/auth/register*` | Public | — |
| `PUT /api/auth/change-password`, `PUT /api/auth/profile` | Any authenticated user | same |
| `/api/admin/*` | Administrator only | Administrator only |
| `/api/appointments` (+ `available-slots`, `check-conflict`) | Veterinarian, ClinicManager, Administrator | ClinicManager, Administrator |
| `/api/quotations` (+ `calculate`, `submit`, `finalize`) | Veterinarian, ClinicManager, Administrator | ClinicManager, Administrator |
| `GET /api/approvals/*` | Veterinarian, ClinicManager, Administrator | — |
| `POST /api/approvals/{id}/approve|reject|revision` | — | **ClinicManager only** |
| `GET /api/examinations|diagnoses|treatment-records|prescriptions` (list) | Veterinarian, ClinicManager, Administrator | — |
| `GET .../{id}` and by-parent clinical reads | PetOwner (own only, enforced in action) + Veterinarian, ClinicManager, Administrator | — |
| `POST|PUT|PATCH|DELETE /api/examinations|diagnoses|treatment-records|prescriptions` | — | Veterinarian, Administrator |
| `GET /api/medicines`, `/low-stock`, `/expiring`, `/{id}` | Veterinarian, ClinicManager, InventoryOfficer, Administrator | — |
| `POST /api/medicines`, `POST /{id}/stock-in` | — | InventoryOfficer, Administrator |
| `GET /api/medicines/{id}/batches`, `/{id}/transactions` | ClinicManager, InventoryOfficer, Administrator | — |
| `GET /api/suppliers[/{id}]` | InventoryOfficer, ClinicManager, Administrator | — |
| `POST /api/suppliers` | — | InventoryOfficer, Administrator |
| `GET /api/medicine-reservations` | Veterinarian, InventoryOfficer, ClinicManager, Administrator | — |
| `POST /api/medicine-reservations`, `/{id}/cancel` | — | Veterinarian, InventoryOfficer, Administrator |
| `POST /api/medicine-reservations/{id}/dispense` | — | InventoryOfficer, Administrator |
| `/api/pets`, `/api/petowners` (GET) | PetOwner (own only) + Veterinarian, ClinicManager, Administrator | — |
| `POST|PUT|DELETE /api/pets`, `POST /api/petowners` | — | PetOwner (own only) + ClinicManager, Administrator |
| `/api/consultations` (GET) | PetOwner (own only) + Veterinarian, ClinicManager, Administrator | — |
| `POST|PUT /api/consultations`, `/submit`, `/cancel` | — | PetOwner (own only) + ClinicManager, Administrator |
| `GET /api/consultations/nearest-clinic` | Any authenticated user (utility) | — |
| `GET /api/lookups/pets` | Veterinarian, ClinicManager, Administrator | — |
| `GET /api/lookups/medicines` | Veterinarian, ClinicManager, InventoryOfficer, Administrator | — |

The InventoryOfficer role is excluded from all pet/consultation/clinical/scheduling/billing data. The PetOwner role is excluded from all inventory, supplier, scheduling, billing, approval, and administration endpoints.

---

## 6. PetOwner ownership enforcement

Ownership is resolved **server-side** from the authenticated identity — never from caller-supplied owner ids:

1. `PetOwner.UserId → User.Id` is a configured one-to-one relationship. Every PetOwner login account maps to exactly one owner profile.
2. `OwnerAccessService` (scoped per request) reads the JWT `sub`/`NameIdentifier` claim, resolves the linked `PetOwner.Id` via `PetOwners.UserId`, and answers ownership questions for the whole graph: `Pet`, `ConsultationRequest`, `Examination`, `Diagnosis`, `TreatmentRecord`, `Prescription`.
3. Controller actions short-circuit PetOwner callers that reference resources they do not own — `404 NotFound` where hiding resource existence is the convention, `403 Forbidden` where the existing contract uses it.
4. On create endpoints (`POST /api/pets`, `POST /api/consultations`, `POST /api/petowners`), any client-supplied `OwnerId`/profile identity is **overwritten** with the caller's resolved owner id.
5. PetOwner registration (`POST /api/auth/register/pet-owner`) creates the `User` account **and** the linked `PetOwner` profile in a single `SaveChangesAsync` (one EF transaction). If a clinic-created unlinked owner profile already exists for the email, it is attached to the new account instead of duplicated.

---

## 7. Organization / tenant scoping

Organization-owned operational data is isolated per tenant:

- `User.OrganizationId` links staff accounts to their `Organization`.
- `ITenantContext` (API-scoped `TenantContext`) resolves the caller's organization from the JWT `sub` → `Users` row once per request.
- `OrganizationId` is carried directly on `Veterinarian`, `Medicine`, and `Supplier`; all other org-owned entities resolve scope transitively: `Appointment`/`AppointmentSlot` → `Veterinarian`, `Quotation` → `Appointment.Veterinarian`, `Approval`/`ApprovalHistory` → `Quotation.Appointment.Veterinarian`, `MedicineBatch`/`MedicineReservation`/`InventoryTransaction` → `Medicine`, and the clinical chain `Examination → Veterinarian` (with `Diagnosis`/`TreatmentRecord`/`Prescription` below it).
- `TenantQueryableExtensions.ScopeToOrganizationAsync` filters every repository query, `GetById` (which backs update/delete loads), and clinical service query for org-scoped callers.
- Create paths validate that referenced parents are in-scope (`veterinarian`, `examination`, `diagnosis`, `treatment record`, `medicine`, `appointment`, `slot`); cross-organization ids fail as `NotFoundException` → 404.
- **SuperAdmin is unscoped** (cross-organization visibility). **PetOwner callers are not org-scoped** — their access is governed by the ownership checks above.
- Rows with `OrganizationId = NULL` are invisible to org-scoped callers; the final migration must backfill legacy rows.

---

## 8. Registration atomicity

| Flow | Boundary |
|---|---|
| `POST /api/auth/register/pet-owner` | `User` + linked `PetOwner` staged, single `SaveChangesAsync` — failure cannot leave a login without an owner profile |
| `POST /api/auth/register/organization` | `Organization` + ClinicManager `User` (`OrganizationId` link) staged, single `SaveChangesAsync` — failure cannot leave an organization without a manager |

`AuditableEntity` generates `Guid` ids client-side, so the `OrganizationId` link is known before insert.

---

---

## 9. React security

### Auth state

- `AuthContext` (`features/auth/AuthContext.tsx`) provides `user`, `isAuthenticated`, `login`, `logout`, and `hasRole` via React Context.
- On mount, it initializes from `authService.getCurrentUser()`, which reads the stored session from `localStorage` and checks expiry.

### Token storage

- `authStorage.ts` stores the session as JSON under `localStorage` key `petcare.auth`.
- `StoredAuth` contains: `token`, `expiresAt`, `userId`, `email`, `name`, `role`.
- `isAuthValid` checks `expiresAt > Date.now()`.

### Protected routes

- `ProtectedRoute` (`features/auth/ProtectedRoute.tsx`) wraps all non-login routes. If `isAuthenticated` is false, it redirects to `/login` with the original location preserved in router state.
- `RoleRoute` gates each route by role, driven by `features/auth/roleAccess.ts` — the single source of truth for `ROLE_HOMES`, the path→roles table, `canRoleAccessPath`, and `safeRedirectPath`.
- Post-login redirects validate the saved `state.from` against the user's role (`safeRedirectPath`); a stale cross-role `from` falls back to the role's home instead of flashing the unauthorized page.
- Role-specific action gating (e.g. showing/hiding approve/reject buttons) is additionally handled at the component level via `hasRole(...)`.

### API authorization

- `apiRequest` in `services/api.ts` attaches `Authorization: Bearer {token}` from stored auth.
- On **401**, the session is cleared from `localStorage` (so the next protected-route check redirects to `/login`).
- On **403**, the session is preserved (403 means "authenticated but not permitted", not "please log in again").

---

## 10. Flutter security

### Token storage

- `TokenStorage` (`core/auth/token_storage.dart`) uses the `flutter_secure_storage` package (platform keychain/keystore-backed).
- Stores `token`, `expiresAt`, `userId`, `email`, `name`, `role` under separate keys prefixed `petcare.*`.
- `getSession()` checks `expiresAt > DateTime.now()` before returning a session.

### Authentication state

- `AuthProvider` (`features/auth/auth_provider.dart`) exposes `AuthStatus` (`unknown`, `authenticated`, `unauthenticated`), `isAuthenticated`, `isClinicManager`, and `errorMessage`.
- On app start, `AuthProvider.init()` calls `AuthService.restoreSession()` to restore from secure storage.
- `PetCareApp` wires `ApiClient.onUnauthorized` to `AuthProvider.logout` so a 401 response automatically signs the user out.

### Protected navigation

- `PetCareApp` shows `LoginPage` when `auth.isAuthenticated` is false and `HomePage` when true.
- `ApiClient` clears its in-memory token on 401 and invokes `onUnauthorized`, which triggers `AuthProvider.logout`.

### API authorization

- `ApiClient` (`core/network/api_client.dart`) attaches `Authorization: Bearer $token` when a token is set.
- On **401**: clears the token and calls `onUnauthorized` (which logs the user out).
- On **403**: returns a specific "Access denied" message but preserves the token.

---

## 11. Security practices actually present

| Practice | Present? | Evidence |
|---|---|---|
| No hardcoded JWT signing key | Yes | `ServiceCollectionExtensions` throws if no key configured; `appsettings*.json` have empty `Key` |
| Environment-based secret fallback | Yes | `PETCARE_JWT_KEY` env var; `PETCARE_DB_CONNECTION` env var |
| Password hashing (PBKDF2) | Yes | `PasswordHasher.cs` — 100,000 iterations, SHA-256, 16-byte salt, 32-byte hash |
| Constant-time password comparison | Yes | `CryptographicOperations.FixedTimeEquals` |
| JWT signature validation | Yes | `ValidateIssuerSigningKey = true` |
| JWT lifetime validation | Yes | `ValidateLifetime = true`, 30s clock skew |
| Role-based authorization | Yes | `[Authorize(Roles = Roles.ClinicManager)]` on approval decisions |
| Frontend protected routes | Yes | React `ProtectedRoute`; Flutter `AuthProvider`-driven navigation |
| 401 session cleanup | Yes | Both clients clear the session/token on 401 |
| 403 session preservation | Yes | Both clients preserve the token on 403 |
| CORS restriction (Development) | Yes | `appsettings.Development.json` allows only `http://localhost:5173` |

---

## 12. Current security limitations

- **No refresh-token flow:** The JWT has a fixed expiry. After expiry, the user must log in again. There is no silent refresh mechanism.
- **`ReviewedBy` is caller-supplied:** The approval decision endpoints accept `ReviewedBy` as a request-body field rather than deriving it from the JWT `sub` claim. The role is enforced, but the reviewer identity is not automatically bound to the authenticated user.
- **Token stored in `localStorage` (React):** This is vulnerable to XSS-based extraction. A production deployment should consider `HttpOnly` cookies or a more hardened storage strategy.
- **Production CORS is empty:** `appsettings.json` has `"AllowedOrigins": []`, so the API will not serve browser clients in a production configuration without explicit configuration.
- **No rate limiting:** The login endpoint has no rate limiting or lockout policy in the current implementation.
- **No HTTPS enforcement in Development:** `app.UseHttpsRedirection()` is present but Development mode does not enforce HTTPS for local testing.
- **EF model is ahead of the schema (pre-migration):** Tenant columns (`Veterinarians/Medicines/Suppliers.OrganizationId`), `PetOwners.UserId`, and the `Appointments.PetId` type change exist only in the model until the final migration runs. Rows with NULL `OrganizationId` are invisible to org-scoped staff and must be backfilled; `PetOwners.UserId` must be backfilled by matching email.
