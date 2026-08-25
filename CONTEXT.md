# CONTEXT — DgDevelopment.Identity

This file provides full project context for AI tools and LLMs operating on the repository.

## Overview

**DgDevelopment.Identity** is a complete Identity Provider for the DgDevelopment ecosystem. It provides authentication (OAuth 2.0 / OIDC, SAML 2.0), authorization (RBAC + PBAC with permissions, roles, hierarchical groups), user profile management, MFA and multi-client SDKs.

## Current State — 2026-08-25

The OAuth 2.0 / OIDC authentication cycle is **fully merged into `develop`** (PRs #1–#33). PRs #34–#38 are also merged: the admin client was renamed `AdminUi` → **`IdentityPlatform`** (with GUID client IDs), an admin authorization foundation landed (`RequirePermissionAttribute`, `IPermissionEvaluator`), a Users administration API + dashboard + Blazor UI shipped, and a **multi-tenant foundation** (`Tenant`/`TenantMembership`, `ITenantContext`, `tid` token claim, tenant switcher UI) made all of the above tenant-aware. PR #40 (`feature/tenant-aware-rbac-api`, in progress) adds the same tenant-aware admin surface for Roles, Permissions, and Groups.

**Milestone M1 is still open**: admin APIs for Clients/Platforms and Audit, and integration tests, are not implemented yet. **No forward merge (`develop` → `docs` → `integration` → `main`) until M1 is complete.**

**TOTP + Push MFA** (merged, PR #33): RFC 6238 enrollment/verification with QR code and hashed single-use backup codes, plus push-mfa primitives (device registry, challenge lifecycle, ANH notifier). MFA login step via partial-authentication cookie (`Identity.Partial`) wired into the password page → `Account/Mfa` page. See `docs/mfa.md`.

### Implemented (merged)

- **OAuth/OIDC engine** (OAuth project): `authorization_code` + PKCE S256, `client_credentials`, `refresh_token` (rotating), `device_code` (token processing); RS256 JWT with active key + N previous keys for validation during rotation.
- **OIDC consent** (PR #24): `Account/Consent` page wired into the authorize flow — shows requested scopes with client name → approve/deny (admin-approved scopes allowed without prompt).
- **Device code flow** (PR #25): `/connect/deviceauthorization` (device + user code issuance, RFC 8628 errors) + `/device` verification/approval UI; `ProcessDeviceCodeAsync` exchanges on poll.
- **Token introspection + revocation**: `/connect/introspect` (RFC 7662) and `/connect/revoke` (RFC 7009), both with `client_id`/`client_secret` authentication. Refresh tokens revoke the whole rotating family; access tokens go to a `RevokedToken` denylist (jti-hashed table) that is enforced by `/connect/userinfo` and introspection.
- **Server**: `ConnectController` (`/connect/token`, `/connect/deviceauthorization`, `/connect/introspect`, `/connect/revoke`, `/connect/jwks`, `/connect/userinfo`, `/connect/endsession`, `/.well-known/openid-configuration`), Razor Pages (login, consent, device approval, tenant selection, error), dynamic CORS (per-client origin whitelist), OpenAPI + Scalar + Swagger.
- **Admin API** (`/api/v1/*`, all `[Authorize(AuthenticationSchemes = "Bearer")]` + `[RequirePermission("identity-platform.{entity}.{action}")]`): `UsersController` (paged list, CRUD, lock/unlock, reset-password, role/permission/group assignment, effective-permissions), `DashboardController` (summary stats), `MeController` (`/me/tenants`), `RolesController`/`GroupsController` (tenant-scoped CRUD + role-permission / group-role assignment), `PermissionsController` (read-only global catalog). Every list/mutation endpoint takes an `allTenants` query flag, honored only for callers holding a global (`IsGlobal`) permission via `ITenantContext.IsGlobalAdministratorAsync`.
- **Multi-tenancy**: `Tenant`/`TenantMembership` entities; `Client`, `Group`, `Role`, `UserRole`, `UserPermission`, `AuditLog`, `UserConsent`, `RefreshToken`, `UserSession`, `AuthorizationCode`, `DeviceCode` all carry a non-nullable `TenantId`. `Permission` stays a global catalog (`IsGlobal` flag instead of a tenant). Active tenant comes from the `tid` access-token claim via `ITenantContext`; switching tenants redoes the OIDC flow (`/login?tenant={slug}`), it's never a client-side variable flip.
- **IdentityPlatform** is a Blazor **Server + WASM hybrid** (renamed from `AdminUi` in PR #34), split into two projects: `DgDevelopment.Identity.IdentityPlatform` (server host) and `DgDevelopment.Identity.IdentityPlatform.Client` (WASM interactive pages/layout: `Home`, `Users`, `UserDetails`, tenant switcher in `MainLayout`).
- **Client SDK** (`Client.Core` + `Client.Blazor`): `IdentityClient`, PKCE, token store in `sessionStorage`, refresh handler, `IdentityAuthStateProvider`, `SessionMarkerService` (`identity_marker` cookie) for SSR prerender restore.
- **Test suite**: `DgDevelopment.Identity.Server.UnitTests` — 354 tests over domain, application, infrastructure, OAuth, and multi-tenant layers on a LocalDB fixture. Coverage HTML auto-generated to `TestResults\html` on every Debug build (ReportGenerator 5.5.11).
- **DB seeding**: permissions (incl. `identity-platform.{user,role,permission,group,tenant,dashboard}.*`), SuperAdmin role (all permissions), SuperAdmins group, superadmin user, `identity-platform` client, all owned by a seeded `Identity Tenant` (slug `identity-tenant`). Stable credentials between runs.

### MFA

- **TOTP (RFC 6238)**: `TotpGenerator` (HMAC-SHA1, 30 s window, 6/8 digits, base32), `TotpSecret` + owned `BackupCode` entities, `TotpSecretProtector` (AES) for the secret key. `TotpService` handles enroll (QR provisioning URI), enable (verifies one TOTP code + issues 10 hashed single-use backup codes), verify (accepts TOTP code or one backup code), regenerate backup codes, disable.
- **Push MFA primitives**: `PushDevice` registry per user, `MfaChallenge` lifecycle (Pending → Approved/Denied, 5 min expiry, 6-digit hashed challenge code), `IPushNotifier` fan-out (heap of notifiers); `AzureNotificationHubNotifier` implements the ANH transport via `Azure:NotificationHub:*` config.
- **MFA login step**: password page → `MfaPolicyService.RequiresMfaStepAsync` decides (user `RequireMfa` flag, TOTP enabled, active push devices) → signs in to short-lived `Identity.Partial` cookie (15 min) → `Account/Mfa` page offers TOTP code, QR enrollment (QRCoder) or push challenge (SignalR hub `/hubs/mfa` polling) → on success, upgrades to the full session cookie with `amr` claims (`pwd`,`totp` / `pwd`,`push`) that flow into the `amr` claim of the ID token.
- **API**: `/api/v1/account/mfa` (push device CRUD, challenge status/approve/deny).
- Tests: `TotpGeneratorTests` (RFC 6238 test vectors), `TotpServiceTests`, `PushMfaServiceTests` on the LocalDB fixture.

### Working end-to-end flow

- IdentityPlatform `/login` → IDP `/connect/authorize` (PKCE S256) → IDP login (static SSR Razor Page) → consent (`Account/Consent`) → authorization code → IdentityPlatform `/callback` → `/connect/token` → tokens saved in `sessionStorage` + `identity_marker` cookie
- Ports are **dynamic** (Aspire binding); `IdentityBaseUrl` / `AdminBaseUrl` / `Identity:AdminClientSecret` come from AppHost/user-secrets
- IdentityPlatform top bar shows avatar (initials) + username + Logout when authenticated, plus a tenant switcher (dropdown for 2+ memberships, a badge otherwise) and an "All tenants" checkbox for global administrators; Home nav is visible only when authenticated
- Session restore: `IdentityAuthStateProvider` (WASM) reads stored tokens, refreshes if expired, fetches `/connect/userinfo` once (cached); `ServerIdentityAuthStateProvider` (prerender) reads the `identity_marker` cookie without network calls
- IDP `/connect/userinfo` returns `sub` / `name` / `email`; client DTOs (`TokenResponse`, `UserInfo`) are aligned to the IDP **snake_case** responses

### Development database

- Migrations run **up-only** on every start (`MigrateAsync`); `EnsureDeletedAsync` was removed
- Seeding runs **once** (first run): superadmin password and admin client secret are **stable** between runs
- `superadmin-credentials.txt` / `admin-client-credentials.txt` are written only on the first seed
- Stale IDP session cookies (user no longer in the DB after a reseed) self-heal: IDP signs out and shows the login form instead of an `invalid_user` error

### Next steps / open work (M1 completion)

Ordered by dependency:

1. **Admin API `/api/v1/*` — remaining entities**: Clients, Platforms, Audit still have no admin endpoints (Users, Dashboard, Roles, Permissions, Groups are done — see PR-D/#40). Planned as PR-E (tenant-aware clients/platforms) and PR-F (tenant-aware audit/introspection/permission claims) in the roadmap.
2. **PR-D-UI**: `Roles.razor`/`RoleDetails.razor`, `Groups.razor`/`GroupDetails.razor` in IdentityPlatform.Client, mirroring `Users.razor`/`UserDetails.razor`.
3. **User management**: registration + email verification, password reset, password policy, lockout enforcement. **`INotificationService`** transport layer (email/notification sending) also not implemented.
4. **Audit Log + Event Store**: entities exist; no admin-facing implementation yet (writes happen via `IAuditService`, no read/query API).
5. **UI pages**: `/profile`, `/logout`, `/mfa` (self-service management; login-time enrollment works), `/profile/emails`; rate limiting (login/token/userinfo) and CSP headers on UI pages.
6. **Tests**: `DgDevelopment.Identity.Server.UnitTests` has 354 tests; `IntegrationTests` project still empty — coverage is written as the features land. PR-G (end-to-end integration tests + full docs pass) is the planned final step of the RBAC/multi-tenant roadmap.

After M1 is complete: **forward merge** `develop` → `docs` → `integration` → `main`, then milestones M2–M4 (SAML 2.0, React/WPF/MAUI client SDKs, Push MFA + external providers, localization integration).

## Architecture Rules

### Clean Architecture — strictly controlled dependencies

```
IdentityPlatform ──> Client.Blazor ──> Client.Core
IdentityPlatform.Client ──> Client.Blazor ──> Client.Core
Server ──> Application ──> Domain <── Infrastructure (implements Domain interfaces)
  │            ▲               ▲
  ├──> OAuth ──┘               │
  └──> Saml ───────────────────┘
        OAuth ──> Application ──> Domain
        Saml ──> Application ──> Domain
AppHost ──> Server, IdentityPlatform
```

- **Domain** references no other projects. Contains entities, value objects, repository interfaces, domain services.
- **Application** references only Domain. Contains use cases, DTOs, application service interfaces.
- **Infrastructure** references Domain (to implement interfaces). Contains EF Core, concrete repositories, external services, security (Argon2 hasher).
- **Server** references Application, Infrastructure, OAuth, Saml, ServiceDefaults. Contains API controllers, Razor Pages (login/consent UI), host configuration, seeding.
- **OAuth** and **Saml** reference Application and Domain. Contain pure protocol logic.
- **Client.Core** has no project references. Contains OIDC client logic, DTOs.
- **Client.Blazor** references Client.Core. Contains Blazor auth components.
- **IdentityPlatform** references Client.Blazor. Blazor Server host (prerender, `ServerIdentityAuthStateProvider`).
- **IdentityPlatform.Client** references Client.Blazor. Blazor WASM interactive pages/layout (login, callback, home, nav).

### Naming Convention C\#

| Element | Convention | Example |
|---|---|---|
| Entity | PascalCase, noun | `User`, `Client`, `Permission` |
| Value Object | PascalCase, noun | `EmailAddress`, `TokenId` |
| Interface | PascalCase, `I` prefix | `IUserRepository`, `ITokenService` |
| DTO / Record | PascalCase, `Dto` or `Record` suffix | `UserDto`, `TokenRequest` |
| Service | PascalCase, `Service` suffix | `TokenService`, `UserService` |
| Controller | PascalCase, `Controller` suffix | `ConnectController`, `UsersController` |
| Command/Query | PascalCase, `Command`/`Query` suffix | `CreateUserCommand`, `GetUserQuery` |
| Handler | PascalCase, `Handler` suffix | `CreateUserHandler` |
| Endpoint | kebab-case, lowercase | `/connect/authorize`, `/api/users/{id}` |
| Private field | `_camelCase` | `_userRepository`, `_dbContext` |
| Async method | `Async` suffix | `GetUserAsync()`, `SaveChangesAsync()` |

### EF Core

- Domain entities are POCO. No dependency on EF Core.
- EF Core configurations (IEntityTypeConfiguration\<T\>) reside in Infrastructure.
- DbContext resides in Infrastructure.
- Migrations are run from the Infrastructure project.
- For multi-provider: all LINQ queries must be compatible with SQL Server and PostgreSQL (no SQL Server-specific functions).
- Uri properties use `.HasConversion(v => v.ToString(), v => new Uri(v))`.
- Enums use `.HasConversion<string>()`.

### API Design

- RESTful, versioning via URL path: `/api/v1/...`
- OAuth/OIDC/SAML routes are not versioned: `/connect/...`, `/saml/...`
- Problem Details (RFC 7807) for errors
- HATEOAS where appropriate

### Security

- Passwords: Argon2id with 64MB RAM, 4 iterations, 4 parallelism
- Client secrets: SHA256 hashed (per OAuth spec)
- Authorization codes: SHA256 hashed
- Refresh tokens: SHA256 hashed, rotating (each use invalidates old token)
- JWT: RS256 signed (RSA 2048-bit), active key + N previous for validation
- PKCE: mandatory for public clients, S256 only

## Project Conventions

### File system

```
src/DgDevelopment.Identity.{Project}/
  ├── {Project}.csproj
  └── (source code)

tests/DgDevelopment.Identity.{TestProject}/
  ├── {TestProject}.csproj
  └── (tests)

clients/DgDevelopment.Identity.Client.{Platform}/
  ├── {Platform}.csproj (or package.json for React)
  └── (client code)
```

### Namespace

- `DgDevelopment.Identity` prefix for everything
- Example: `DgDevelopment.Identity.Domain.Entities`, `DgDevelopment.Identity.Application.Users`

### Code Quality

- `Directory.Build.props` sets `AnalysisMode=AllEnabledByDefault` and `EnforceCodeStyleInBuild=true`
- All analyzers produce warnings (not errors)
- Domain project suppresses CS8618 (EF Core private constructors), CA1711 (Permission naming), CA1721 (Scopes/GetScopes)
- No pragma suppressions — all warnings either fixed or globally suppressed with documented reason

## Useful Commands

```bash
# Build
dotnet build

# Test
dotnet test

# Run with Aspire
aspire run

# EF Core Migrations
dotnet ef migrations add <Name> --project src/DgDevelopment.Identity.Infrastructure --startup-project src/DgDevelopment.Identity.Server

# Lint / Format
dotnet format
```

## Branch Strategy

### Branches

```
main ──── Production
  ▲
integration ──── Third-party integration testing
  ▲
docs ──── API documentation + generated OpenAPI
  ▲
develop ──── Active development (base for all feature/fix branches)
  ▲
feature/*  fix/*
```

### Rules

- `main`, `integration`, `docs`: **no direct commits** — only merges from lower branches.
- `develop`: feature/fix branches merged via **PR + squash merge** (1 approval required).
- **Merge forward**: `develop` → `docs` → `integration` → `main` (when promoted).
- **Merge backward**: hotfixes on `main` flow back: `main` → `integration` → `docs` → `develop`.
- Branch naming: `feature/<description>` or `fix/<description>` (kebab-case, English).
- Stacked PRs target the parent feature branch, not develop directly.

### Commit Convention

```
<type>: <short description>
```

Types: `feat`, `fix`, `docs`, `refactor`, `test`, `chore`, `perf`, `style`.

See `BRANCHING.md` for detailed flow and examples.

## Notes for AI / LLM

- Do not add code comments unless explicitly requested.
- Follow existing conventions.
- Do not introduce dependencies not already present in the project without verifying first.
- `.csproj` files must use `TargetFramework` `net10.0`.
- Sensitive configurations (connection strings, keys) must NEVER be committed.
- Stacked PRs: when a feature depends on another in-progress feature, create the branch from the parent feature and set the PR base to the parent branch.
