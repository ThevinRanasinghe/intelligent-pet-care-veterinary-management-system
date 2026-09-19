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
| `Roles.ClinicManager` | `"ClinicManager"` | Can approve/reject/request-revision on quotations |
| `Roles.Staff` | `"Staff"` | General authenticated staff member |

### Role-based protection

The three approval decision endpoints in `ApprovalsController` are decorated with `[Authorize(Roles = Roles.ClinicManager)]`:

| Endpoint | Authorization |
|---|---|
| `POST /api/approvals/{id}/approve` | `[Authorize(Roles = Roles.ClinicManager)]` |
| `POST /api/approvals/{id}/reject` | `[Authorize(Roles = Roles.ClinicManager)]` |
| `POST /api/approvals/{id}/revision` | `[Authorize(Roles = Roles.ClinicManager)]` |

All other endpoints require only authentication (a valid JWT). There is no `[AllowAnonymous]` on any endpoint except `POST /api/auth/login` (which has no `[Authorize]` attribute).

---

## 6. Protected API operations

| Operation | Protection level |
|---|---|
| `POST /api/auth/login` | Public (no auth required) |
| `GET /api/appointments/*`, `POST /api/appointments`, `PUT /api/appointments/{id}`, `DELETE /api/appointments/{id}` | Authenticated |
| `GET /api/quotations/*`, `POST /api/quotations`, `PUT /api/quotations/{id}`, `POST /api/quotations/{id}/*` | Authenticated |
| `GET /api/approvals/pending`, `GET /api/approvals/{id}`, `GET /api/approvals/{id}/history` | Authenticated |
| `POST /api/approvals/{id}/approve` | **ClinicManager only** |
| `POST /api/approvals/{id}/reject` | **ClinicManager only** |
| `POST /api/approvals/{id}/revision` | **ClinicManager only** |

---

## 7. React security

### Auth state

- `AuthContext` (`features/auth/AuthContext.tsx`) provides `user`, `isAuthenticated`, `login`, `logout`, and `hasRole` via React Context.
- On mount, it initializes from `authService.getCurrentUser()`, which reads the stored session from `localStorage` and checks expiry.

### Token storage

- `authStorage.ts` stores the session as JSON under `localStorage` key `petcare.auth`.
- `StoredAuth` contains: `token`, `expiresAt`, `userId`, `email`, `name`, `role`.
- `isAuthValid` checks `expiresAt > Date.now()`.

### Protected routes

- `ProtectedRoute` (`features/auth/ProtectedRoute.tsx`) wraps all non-login routes. If `isAuthenticated` is false, it redirects to `/login` with the original location preserved in router state.
- Role-specific gating (e.g. showing/hiding approve/reject buttons) is handled at the component level in `ApprovalPage` via `hasRole('ClinicManager')`, not in `ProtectedRoute`.

### API authorization

- `apiRequest` in `services/api.ts` attaches `Authorization: Bearer {token}` from stored auth.
- On **401**, the session is cleared from `localStorage` (so the next protected-route check redirects to `/login`).
- On **403**, the session is preserved (403 means "authenticated but not permitted", not "please log in again").

---

## 8. Flutter security

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

## 9. Security practices actually present

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

## 10. Current security limitations

- **No refresh-token flow:** The JWT has a fixed expiry. After expiry, the user must log in again. There is no silent refresh mechanism.
- **`ReviewedBy` is caller-supplied:** The approval decision endpoints accept `ReviewedBy` as a request-body field rather than deriving it from the JWT `sub` claim. The role is enforced, but the reviewer identity is not automatically bound to the authenticated user.
- **Token stored in `localStorage` (React):** This is vulnerable to XSS-based extraction. A production deployment should consider `HttpOnly` cookies or a more hardened storage strategy.
- **Production CORS is empty:** `appsettings.json` has `"AllowedOrigins": []`, so the API will not serve browser clients in a production configuration without explicit configuration.
- **No rate limiting:** The login endpoint has no rate limiting or lockout policy in the current implementation.
- **No HTTPS enforcement in Development:** `app.UseHttpsRedirection()` is present but Development mode does not enforce HTTPS for local testing.
