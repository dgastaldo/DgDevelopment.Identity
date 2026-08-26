# CONTEXT — DgDevelopment.Identity

This file provides full project context for AI tools and LLMs operating on the repository.

## Overview

**DgDevelopment.Identity** is a complete Identity Provider for the DgDevelopment ecosystem. It provides authentication (OAuth 2.0 / OIDC, SAML 2.0), authorization (RBAC + PBAC with permissions, roles, hierarchical groups), user profile management, MFA and multi-client SDKs.

## Current State — 2026-08-26

The OAuth 2.0 / OIDC authentication cycle is **fully merged into `develop`** (PRs #1–#33). PRs #34–#41 are also merged: the admin client was renamed `AdminUi` → **`IdentityPlatform`** (with GUID client IDs), an admin authorization foundation landed (`RequirePermissionAttribute`, `IPermissionEvaluator`), a Users administration API + dashboard + Blazor UI shipped, a **multi-tenant foundation** (`Tenant`/`TenantMembership`, `ITenantContext`, `tid` token claim, tenant switcher UI) made all of the above tenant-aware, a tenant-aware **Roles/Permissions/Groups admin API** shipped, and the **`IntegrationTests` project** was brought forward with real-HTTP end-to-end coverage.

**A stacked PR chain is open and ready for review, not yet merged**: `#43` (base `develop`, tenant-aware Clients/Platforms API) → `#44` (audit log API, real permission claims, tenant-aware introspection) → `#45` (Roles/Groups admin UI + permission-based nav/page gating) → `#46` (Clients/Platforms/Audit admin UI + user role/permission/group assignment). Together these close out **all remaining M1 admin API and admin UI work**. Merge order matters (each PR's branch is the next one's base); once `#43` merges and its branch is deleted, GitHub auto-retargets `#44` onto `develop`, and so on down the chain.

**A further branch is stacked on top of `#46`**: `feature/platform-scoped-roles-permissions`, not yet opened as a PR. It makes `Role` and `Permission` platform-scoped (see the "Permissions are platform-scoped" bullet below) and adds a `TenantProvisioningService`/`TenantsController` for creating new tenants. This is the most invasive schema change since the multi-tenant foundation — it touches the migration chain, every Role/Permission constructor call site, and the Role/Permission admin UI — so it should merge only after the `#43`–`#46` chain is in `develop`.

**Milestone M1 is not yet fully closed** even once that chain merges — see "Next steps" below for what's genuinely still missing (self-service pages, registration/password-reset flows, rate limiting/CSP, Event Store UI). **No forward merge (`develop` → `docs` → `integration` → `main`) until M1 is complete.**

**TOTP + Push MFA** (merged, PR #33): RFC 6238 enrollment/verification with QR code and hashed single-use backup codes, plus push-mfa primitives (device registry, challenge lifecycle, ANH notifier). MFA login step via partial-authentication cookie (`Identity.Partial`) wired into the password page → `Account/Mfa` page. See `docs/mfa.md`.

### Implemented

Describes the current code, regardless of git merge state — most of this is in `develop`; anything specifically from the `#43`→`#44`→`#45`→`#46` chain is called out by PR number below and is only merged once that chain lands (see "Current State" above).

- **OAuth/OIDC engine** (OAuth project): `authorization_code` + PKCE S256, `client_credentials`, `refresh_token` (rotating), `device_code` (token processing); RS256 JWT with active key + N previous keys for validation during rotation.
- **OIDC consent** (PR #24): `Account/Consent` page wired into the authorize flow — shows requested scopes with client name → approve/deny (admin-approved scopes allowed without prompt).
- **Device code flow** (PR #25): `/connect/deviceauthorization` (device + user code issuance, RFC 8628 errors) + `/device` verification/approval UI; `ProcessDeviceCodeAsync` exchanges on poll.
- **Token introspection + revocation**: `/connect/introspect` (RFC 7662) and `/connect/revoke` (RFC 7009), both with `client_id`/`client_secret` authentication. Refresh tokens revoke the whole rotating family; access tokens go to a `RevokedToken` denylist (jti-hashed table) that is enforced by `/connect/userinfo` and introspection.
- **Server**: `ConnectController` (`/connect/token`, `/connect/deviceauthorization`, `/connect/introspect`, `/connect/revoke`, `/connect/jwks`, `/connect/userinfo`, `/connect/endsession`, `/.well-known/openid-configuration`), Razor Pages (login, consent, device approval, tenant selection, error), dynamic CORS (per-client origin whitelist), OpenAPI + Scalar + Swagger.
- **Admin API** (`/api/v1/*`, all `[Authorize(AuthenticationSchemes = "Bearer")]` + `[RequirePermission("identity-platform.{entity}.{action}")]`): `UsersController` (paged list, CRUD, lock/unlock, reset-password, role/permission/group assignment, effective-permissions — merged), `DashboardController` (summary stats — merged), `MeController` (`/me/tenants` — merged), `RolesController`/`GroupsController` (tenant-scoped CRUD + role-permission / group-role assignment — merged, PR #40), `PermissionsController` (read-only global catalog — merged), `ClientsController` (tenant-scoped CRUD, secret regeneration, activate/deactivate, full replace-on-update of grant types/scopes/redirect URIs/post-logout URIs/admin-consent scopes, cross-tenant platform-assignment check — **#43, not yet merged**), `PlatformsController` (tenant-scoped CRUD — **#43**), `AuditController` (tenant-scoped, paged, filterable by actor/action/target/date range — **#44**). Every list/mutation endpoint takes an `allTenants` query flag, honored only for callers holding a global (`IsGlobal`) permission via `ITenantContext.IsGlobalAdministratorAsync`.
- **`Platform` vs `Client`** (**#43**): not redundant — the relationship is one `Platform` to many `Client`s (`HasOne<Platform>().WithMany()` on `Client.PlatformId`). A `Platform` is the target *application* (e.g. "IdentityPlatform", "MobileApp") that one or more OAuth clients authenticate into (a confidential web client, a public PKCE client, a machine-to-machine client_credentials client — all separate `Client` registrations for the same underlying application). `Platform.PermissionMode` (`AuthOnly` vs `IdentityManaged`) is meant to record, per application, whether that application's *internal* roles/permissions are managed entirely by the application itself (`AuthOnly` — Identity is just doing authentication for it) or by this Identity platform (`IdentityManaged` — e.g. the seeded "IdentityAdmin" platform, whose roles/permissions/groups live here). **Not wired into any authorization logic yet** — currently pure metadata, set once by the admin UI/seeding and never read back by anything else. Documented here specifically so this distinction doesn't get lost before the behavior is actually implemented.
- **`ClientRepository.UpdateAsync`** (**#43**) needed a different fix than the PR #40 repositories: `Client`'s grant types/scopes/redirect URIs are EF `OwnsMany` owned collections, not independent join-entity types like `RolePermission`/`GroupRole` — reattaching a disconnected graph via `Update()` silently drops additions/removals for owned collections too (verified empirically), but the fix is to load the tracked instance and replay the diff through the entity's own `Add*`/`Remove*` methods, not to reconcile a `DbSet<TOwned>` directly.
- **Access-token `permission` claim** (**#44**; previously dead code — `JwtService`/`ConnectController` had the transport plumbing but `TokenService` always passed `null`): `TokenService.GenerateTokensAsync` now calls `IPermissionEvaluator.GetEffectivePermissionsAsync(user.Id, tenantId)` and stamps one `"permission"` claim per name (not a single space-joined claim - `TokenIntrospectionService` reads it back via `FindAll`, which needs multiple claims of the same type to work). `client_credentials` tokens still carry no permissions (no real user). Introspection (`/connect/introspect`) now also returns `tid` for both access and refresh tokens, so a resource server can learn the token's tenant without decoding the JWT itself.
- **`JwtService.ValidateTokenAsync`** (**#44**) now sets `MapInboundClaims = false` on its `JwtSecurityTokenHandler`, matching the JWT bearer pipeline in `Program.cs`: the default `true` silently renames well-known short claim names ("tid", "sub") to Microsoft's Azure AD claim URIs, breaking any code reading them back by their original name. This is a separate internal re-validation path (introspection, revocation, `/connect/userinfo`) from the ASP.NET Core authentication pipeline, so it needed the same fix independently - found via `IntrospectionResponse.Tid` coming back null, then a second instance found in `/connect/userinfo`'s own `sub` claim read (fixed with a `Sub` claim fallback before `ClaimTypes.NameIdentifier`).
- **Multi-tenancy**: `Tenant`/`TenantMembership` entities; `Client`, `Group`, `Role`, `Permission`, `UserRole`, `UserPermission`, `AuditLog`, `UserConsent`, `RefreshToken`, `UserSession`, `AuthorizationCode`, `DeviceCode` all carry a non-nullable `TenantId`. Active tenant comes from the `tid` access-token claim via `ITenantContext`; switching tenants redoes the OIDC flow (`/login?tenant={slug}`), it's never a client-side variable flip.
- **Permissions are platform-scoped** (**`feature/platform-scoped-roles-permissions`**, not yet merged): `Permission` gained a mandatory `PlatformId` (and `TenantId`, previously it had neither — it used to be a single global catalog). `Role` also gained a mandatory `PlatformId`. `RoleService.AssignPermissionAsync` enforces that a permission can only be attached to a role belonging to the *same* platform (`InvalidOperationException` → 403 otherwise) — a real enforcement constraint, not just informational tagging. This means the permission catalog is now **duplicated per tenant**: every tenant gets its own copy of the "IdentityAdmin" platform and its own ~32-row `identity-platform.*` permission catalog (rows share the same `Name` string across tenants but have distinct `Id`/`TenantId`/`PlatformId` — safe because `IPermissionEvaluator.HasPermissionAsync` matches by name, not by a single canonical `Permission.Id`). `Permission.IsGlobal` is orthogonal to this: it still means "grants visibility across tenants/platforms" (e.g. `identity-platform.tenant.create`), independent of which platform's catalog the permission itself belongs to. Provisioning this per-tenant catalog is what `TenantProvisioningService` (below) exists to do — hand-seeding it per tenant would have been unmaintainable.
- **`TenantProvisioningService`** (`Application/Tenants`, **`feature/platform-scoped-roles-permissions`**): the only supported way to create a new tenant. `ProvisionAsync(name, slug)` creates the `Tenant`, an "IdentityAdmin" `Platform` (`PermissionMode.IdentityManaged`), the full permission catalog scoped to that platform, a `SuperAdmin` role with every permission, and a `SuperAdmins` group with that role — atomically, so a tenant is never left half-provisioned. `DbSeeder` now calls this for the bootstrap "Identity Tenant" instead of hand-seeding each piece separately; `TenantsController` (`POST /api/v1/tenants`, `identity-platform.tenant.create`, `IsGlobal`) exposes it over HTTP for creating additional tenants. No admin UI page exists yet for this — API/service only.
- **IdentityPlatform** is a Blazor **Server + WASM hybrid** (renamed from `AdminUi` in PR #34), split into two projects: `DgDevelopment.Identity.IdentityPlatform` (server host) and `DgDevelopment.Identity.IdentityPlatform.Client` (WASM interactive pages/layout). Pages: `Home` (dashboard), `Users`/`UserDetails` (merged; role/permission/group assignment on the details page is **#46**), `Roles`/`RoleDetails` and `Groups`/`GroupDetails` (**#45**), `Clients`/`ClientDetails`, `Platforms`/`PlatformDetails` and `AuditLog` (**#46**), tenant switcher in `MainLayout` (merged). Every admin page is wrapped in a `RequirePermission` component and hidden from `NavMenu` unless the caller's access token carries the matching `permission` claim - decoded client-side off the token (`JwtClaimsReader`, unverified, since every API call is still enforced server-side); this is UI-hiding only, not a security boundary (**#45**). No dedicated page exists for browsing the `Permissions` catalog on its own — it's only ever shown as a dropdown inside Role/User assignment; low priority since it's read-only and small (~30 rows).
- **Client SDK** (`Client.Core` + `Client.Blazor`): `IdentityClient`, PKCE, token store in `sessionStorage`, refresh handler, `IdentityAuthStateProvider`, `SessionMarkerService` (`identity_marker` cookie) for SSR prerender restore (merged). `IdentityAuthStateProvider.BuildPrincipal` also stamps `permission` claims from the access token (optional parameter, absent during SSR prerender - gated nav briefly hides until the WASM client goes interactive) — **#45**.
- **Test suite**: `DgDevelopment.Identity.Server.UnitTests` — 372 tests (354 merged via PR #41; the rest land with the `#43`–`#46` chain) over domain, application, infrastructure, OAuth, and multi-tenant layers on a LocalDB fixture. Coverage HTML auto-generated to `TestResults\html` on every Debug build (ReportGenerator 5.5.11). `DgDevelopment.Identity.IntegrationTests` (merged, PR #41) — 10 tests driving the real IDP over `WebApplicationFactory<Program>` (login → password → consent → `/connect/token`, scripted with raw `HttpClient`, no Selenium/Aspire), on its own LocalDB (`DgDevelopment.Identity.IntegrationTests`); covers admin API CRUD, cross-tenant 403s, audit log visibility (**#44**), `/connect/userinfo`, and CORS/auth pipeline end-to-end.
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

1. **Merge the `#43`→`#44`→`#45`→`#46` chain**: nothing left to build here, just review + squash-merge in order (base first, GitHub retargets the rest automatically once each parent's branch is deleted).
2. **Open the PR for `feature/platform-scoped-roles-permissions`** (stacked on `#46`) and merge it after the chain above. A `Tenants.razor` admin page (list + create, backed by the already-implemented `TenantsController`/`ITenantProvisioningService`) would be a natural follow-up but is not built yet — currently API/service only.
3. **Self-service pages**: no `/profile`, `/logout`, `/mfa` (self-service device/backup-code management — login-time enrollment already works), or `/profile/emails`. Genuinely not started; a separate, more heterogeneous piece of work than the admin CRUD pages above (MFA self-service touches enrollment/backup-codes/push-device UI, not just forms over an API).
4. **Self-registration and password recovery**: no `/register`, no email verification, no "forgot password" flow — none of these endpoints exist at all (verified: no controller, no service method). Only an *admin-driven* password reset exists today (`UsersController.ResetPassword`, requires `identity-platform.user.update`). **`INotificationService`** (the transport layer any of this would need to send email) also doesn't exist — not even as an interface, nothing to swap out.
5. **Password policy**: `UserService.CreateAsync`/`ResetPasswordAsync` only reject null/whitespace — no length, complexity, or history rules.
6. **Lockout enforcement — already implemented**, contrary to what this file used to say: `UserAuthenticationService` locks an account for 15 minutes after 5 failed password attempts (`MaxFailedAttempts`), independent of the manual admin lock/unlock in the Users API. Nothing to do here.
7. **Event Store**: entity and repository (`IEventStoreRepository`) exist and are registered in DI, but nothing ever calls them — no service writes events, nothing reads them back, no API or UI. Fully dormant scaffolding, not just "missing UI" the way Audit Log was before #44.
8. **Rate limiting and CSP headers**: not implemented on any UI page or the login/token/userinfo endpoints.
9. **Docs pass**: a full documentation pass is still the planned final step before the M1 forward-merge, once everything above (or whatever subset is decided) is either done or explicitly deferred past M1.

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
