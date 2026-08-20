# MFA — DgDevelopment.Identity

## Overview

Multi-factor authentication adds a second authentication step after the username/password check. Two providers are supported:

- **TOTP** (RFC 6238) — time-based one-time passwords via an authenticator app and single-use backup codes.
- **Push** — a challenge pushed to registered devices; the user approves/denies it from their phone.

Feature is implemented on `feature/totp-mfa` (PR pending). TOTP is complete; push MFA has full service/test coverage but the final ANH (Azure Notification Hubs) transport requires `Azure:NotificationHub:*` configuration to go live end-to-end.

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
- `IMfaPolicyService` — `RequiresMfaStepAsync` (combines forced flag, TOTP, push devices).
- `MfaModels` — `TotpEnrollment`, `TotpStatus`, `BackupCodeResult`, `PushDeviceDto`, `PushChallengeCreated`.

### Infrastructure (`Infrastructure`)

- `TotpSecretProtector` — AES encryption for the TOTP secret.
- `AzureNotificationHubNotifier` — `IPushNotifier` via Azure Notification Hubs (`Azure:NotificationHub:ConnectionString`, `Azure:NotificationHub:Name`).
- EF Core configs for `TotpSecret`/`BackupCode` (owned), `PushDevice`, `MfaChallenge`; repository implementations.
- Migration `20260818162646_AddMfaEnrollment` adds `Users.RequireMfa`, `MfaChallenges`, `PushDevices`.

### Server (`Server`)

- `Account/Mfa` page (partial-auth protected), `Account/Password` MFA step, `MfaController` (`/api/v1/account/mfa`), SignalR `MfaHub` at `/hubs/mfa`, partial cookie scheme `Identity.Partial` (15 min).

## Backup codes

- 10 codes generated on enable/regenerate; each is a random 6-byte value base32-encoded, displayed as `XXXXX-XXXXX`.
- Stored as the SHA-256 hash of the normalized code (dashes/spaces stripped, uppercase) — plaintext never persisted.
- Each code is single-use; `VerifyAsync` falls back to backup codes when the TOTP code is invalid.

## Tests

`DgDevelopment.Identity.Server.UnitTests` on the LocalDB fixture:

- `TotpGeneratorTests` — RFC 6238 published test vectors (step counter `T = floor(seconds / 30)`, 6-digit values).
- `TotpServiceTests` — enroll/enable/verify/disable, backup-code issue, redeem-once, regenerate.
- `PushMfaServiceTests` — device idempotency, challenge lifecycle, approve/deny with code, expiry.