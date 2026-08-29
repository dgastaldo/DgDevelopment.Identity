# MFA — DgDevelopment.Identity

## Overview

Multi-factor authentication adds a second authentication step after the username/password check. Two providers are supported:

- **TOTP** (RFC 6238) — time-based one-time passwords via an authenticator app and single-use backup codes.
- **Push** — a challenge pushed to registered devices; the user approves/denies it from their phone.

TOTP is complete and merged into `develop`; push MFA has full service/test coverage but the final ANH (Azure Notification Hubs) transport requires `Azure:NotificationHub:*` configuration to go live end-to-end.

As of M1, TOTP/push MFA is also **mandatory** (not just optional) for `GlobalAdmin`/`SuperAdmin` role holders - see "Mandatory MFA for privileged roles" below.

## Flow

1. **Password page** (`Account/Password`) validates credentials via `IUserAuthenticationService.ValidateCredentialsAsync`.
2. `MfaPolicyService.RequiresMfaStepAsync(user)` decides whether a second step is needed:
   - user `RequireMfa` flag is set, _or_
   - TOTP is enabled for the user (`ITotpService.IsEnabledAsync`), _or_
   - the user has at least one active push device (`IPushMfaService.HasActiveDevicesAsync`).
3. If needed, the user is signed into the **partial authentication cookie** (`Identity.Partial`, 15 min, claim `amr=pwd` + `remember_me`) and redirected to `Account/Mfa`.
4. The `Account/Mfa` page offers three paths:
   - **TOTP code** → `POST totp` → `ITotpService.VerifyAsync` (accepts a current TOTP code or an unused backup code) → `CompleteLoginAsync(["pwd","totp"])`.
   - **Enrollment** (shown when neither TOTP nor push device exists): QR code (`otpauth://` URI via `TotpGenerator.BuildProvisioningUri`, rendered with QRCoder) → user scans and enters one code → `POST enroll` → `EnableAsync` verifies the code and issues 10 hashed single-use backup codes.
   - **Push challenge** → `POST start-push` → `IPushMfaService.StartChallengeAsync` creates a `Pending` `MfaChallenge` (5 min expiry, 6-digit hashed code) and fans out a `ChallengeNew` notification to all active devices through `IPushNotifier`. The page polls `GET /api/v1/account/mfa/challenge/{id}` (or a SignalR `MfaHub` event) until `Approved`, then `POST complete-push` → `IsChallengeApprovedForUserAsync` → `CompleteLoginAsync(["pwd","push"])`.
5. `CompleteLoginAsync` creates the real server session (`IServerSessionService.CreateAsync` with the `amr` methods), issues the full session cookie, and removes the partial cookie. The `amr` methods propagate into the `amr` claim of the ID token (`TokenService` reads `UserSession.GetAuthMethods()`).

Push resolution (approve/deny) is done from the phone app through `POST /api/v1/account/mfa/push/{challengeId}/approve|deny` (authenticated with the full session cookie).

## Self-service (M1)

The login-time flow above is enrollment/verification *during authentication*. Once signed in, a user can manage their own MFA without a fresh login:

- **`/mfa`** (`IdentityPlatform` Blazor page, and `Client.Maui`'s `MfaPage`) - TOTP enroll/enable/disable and push-device register/remove, via `IdentityClient`'s self-service methods (`GetTotpStatusAsync`/`EnrollTotpAsync`/`EnableTotpAsync`/`DisableTotpAsync`/`RegenerateBackupCodesAsync`, `GetPushDevicesAsync`/`RegisterPushDeviceAsync`/`RemovePushDeviceAsync`).
- **`MfaController`** (`/api/v1/account/mfa/*`) and **`MeController`** now accept **both** Cookie and Bearer auth - `MfaController` used to be Cookie-only, which 401'd every call from the Blazor client's Bearer-only `HttpClient`.
- QR rendering is shared between the login-time `Account/Mfa` page and the self-service page via `TotpQrCodeRenderer`.

## Mandatory MFA for privileged roles (M1)

`GlobalAdmin` (tenant admin) and `SuperAdmin` (platform admin) role holders must have an MFA method enrolled - regular users are unaffected.

- **Per-tenant, not global**: the requirement follows the role held *in the tenant being authenticated into*, evaluated by `IUserAuthorizationRepository.HasAnyRoleAsync` (direct role assignment or - possibly nested - group membership). A user who's `GlobalAdmin` in tenant A but a plain member in tenant B only needs MFA for tenant A.
- **Checked in `AuthorizeModel.OnGetAsync`**, right after tenant resolution - the earliest point in the OIDC flow the target tenant (and thus the applicable role) is known. The password/MFA-step-up pages earlier in the flow are tenant-agnostic by construction, so they can't do this check.
- **`MfaEnforcementService.MustEnrollMfaBeforeProceedingAsync`** (Application) is the decision point. A privileged user with no MFA method enrolled gets a **14-calendar-day grace period** (`User.MfaGracePeriodStartedAt`, started the first time this situation is observed for that user) - no hard cutover the moment the rule ships. Once the grace period elapses, they're redirected to **`/account/mfa-enroll`** (full-session auth, reuses `ITotpService`'s enrollment primitives directly - not the login-time partial-auth `Account/Mfa` page) instead of reaching the dashboard.
- **No break-glass exemption**, including for the seeded bootstrap `SuperAdmin` - it goes through the same grace period as everyone else, which is what keeps this safe (it always gets to enroll before enforcement can start).
- **Recovery is TOTP backup codes only** - no email-based or admin-assisted reset if a privileged user loses their device *and* every backup code. Accepted, known limitation.

## Real-time session events

Unrelated to the login-time MFA flow, but related infrastructure: `/hubs/session` (`SessionHub`) pushes a `"ForceLogout"` event to a user's connected clients when their sessions are revoked (e.g. on password change) - see `docs/client-sdk.md`'s "Real-time session events" section and the CONTEXT.md session-revocation bullet. It's a separate hub from `/hubs/mfa` (`MfaHub`, `Identity.Partial`-only, used mid-login before a full session exists) since `SessionHub` is for already-fully-signed-in clients and accepts both Cookie and Bearer auth.

## Components

### Domain (`Domain`)

- `TotpGenerator` — HMAC-SHA1, 30-second step, 6/8 digits, base32 secrets, `otpauth://` provisioning URI builder.
- `TotpSecret` (+ owned `BackupCode`) — secret storage (protected), enable/disable, single-use backup-code redemption, regeneration.
- `PushDevice` — per-user device registry (platform, push token, name, active flag, last seen).
- `MfaChallenge` — push challenge lifecycle (`Pending → Approved/Denied`), 5-min expiry, hashed 6-digit code.
- `ISecretProtector` / `IPushNotifier` — ports implemented in Infrastructure.
- Repositories: `ITotpSecretRepository`, `IPushDeviceRepository`, `IMfaChallengeRepository`.

### Application (`Application`)

- `ITotpService` — enroll, enable, verify (TOTP or backup code), regenerate backup codes, disable, status.
- `IPushMfaService` — device registration/removal, challenge start/approve/deny/status, `HasActiveDevicesAsync`.
- `IMfaPolicyService` — `RequiresMfaStepAsync` (combines forced flag, TOTP, push devices) - decides the login-time step-up.
- `IMfaEnforcementService` — `MustEnrollMfaBeforeProceedingAsync` - decides mandatory-enrollment-for-privileged-roles (see above). A different concern from `IMfaPolicyService`: this one is about whether MFA must be *set up at all*, not whether to challenge for it during this login.
- `MfaModels` — `TotpEnrollment`, `TotpStatus`, `BackupCodeResult`, `PushDeviceDto`, `PushChallengeCreated`.

### Infrastructure (`Infrastructure`)

- `TotpSecretProtector` — AES encryption for the TOTP secret.
- `AzureNotificationHubNotifier` — `IPushNotifier` via Azure Notification Hubs (`Azure:NotificationHub:ConnectionString`, `Azure:NotificationHub:Name`).
- EF Core configs for `TotpSecret`/`BackupCode` (owned), `PushDevice`, `MfaChallenge`; repository implementations.
- Migration `20260818162646_AddMfaEnrollment` adds `Users.RequireMfa`, `MfaChallenges`, `PushDevices`.

### Server (`Server`)

- `Account/Mfa` page (partial-auth protected, login-time step-up/enrollment), `Account/MfaEnroll` page (full-session auth, mandatory-enrollment redirect target), `Account/Password` MFA step, `MfaController`/`MeController` (`/api/v1/account/mfa`, `/api/v1/me` - both Cookie+Bearer), SignalR `MfaHub` at `/hubs/mfa` (login-time, `Identity.Partial`) and `SessionHub` at `/hubs/session` (post-login, `Cookie,Bearer`), partial cookie scheme `Identity.Partial` (15 min).

## Backup codes

- 10 codes generated on enable/regenerate; each is a random 6-byte value base32-encoded, displayed as `XXXXX-XXXXX`.
- Stored as the SHA-256 hash of the normalized code (dashes/spaces stripped, uppercase) — plaintext never persisted.
- Each code is single-use; `VerifyAsync` falls back to backup codes when the TOTP code is invalid.

## Tests

`DgDevelopment.Identity.Server.UnitTests` on the LocalDB fixture:

- `TotpGeneratorTests` — RFC 6238 published test vectors (step counter `T = floor(seconds / 30)`, 6-digit values).
- `TotpServiceTests` — enroll/enable/verify/disable, backup-code issue, redeem-once, regenerate.
- `PushMfaServiceTests` — device idempotency, challenge lifecycle, approve/deny with code, expiry.
- `MfaEnforcementServiceTests` — fresh privileged user not blocked immediately, non-privileged user never asked, grace period elapsed → must enroll, grace period tracked correctly.
- `UserAuthorizationRepositoryRoleTests` — `HasAnyRoleAsync` via direct assignment and via (nested) group membership, tenant isolation.

`DgDevelopment.Identity.IntegrationTests` (real HTTP against `WebApplicationFactory<Program>`):

- `MfaControllerSelfServiceTests` — self-service TOTP enroll/enable/status/backup-codes over Bearer auth (the regression test for the Cookie-only auth-scheme bug).
- `MandatoryMfaEnrollmentTests` — the actual `/connect/authorize` redirect to `/account/mfa-enroll` once the grace period elapses, and that completing real TOTP enrollment there (compute a valid code via `TotpGenerator.ComputeCode`) unblocks a subsequent login.
- `SessionEventHubTests` — a *real* `HubConnection` (forced onto long polling, `TestServer` doesn't support real WebSockets) against `SessionHub`, proving a password change actually pushes `"ForceLogout"` to a subscribed client.