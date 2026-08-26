# CONTEXT — DgDevelopment.Identity

This file provides full project context for AI tools and LLMs operating on the repository.

## Overview

**DgDevelopment.Identity** is a complete Identity Provider for the DgDevelopment ecosystem. It provides authentication (OAuth 2.0 / OIDC, SAML 2.0), authorization (RBAC + PBAC with permissions, roles, hierarchical groups), user profile management, MFA and multi-client SDKs.

## Current State — 2026-08-26

The OAuth 2.0 / OIDC authentication cycle is **fully merged into `develop`** (PRs #1–#33). PRs #34–#47 are also merged: the admin client was renamed `AdminUi` → **`IdentityPlatform`** (with GUID client IDs), an admin authorization foundation landed (`RequirePermissionAttribute`, `IPermissionEvaluator`), a Users administration API + dashboard + Blazor UI shipped, a **multi-tenant foundation** (`Tenant`/`TenantMembership`, `ITenantContext`, `tid` token claim, tenant switcher UI) made all of the above tenant-aware, a tenant-aware **Roles/Permissions/Groups/Clients/Platforms/Audit admin API + UI** shipped, `Role`/`Permission` became platform-scoped with a real `TenantProvisioningService`, and the **`IntegrationTests` project** was brought forward with real-HTTP end-to-end coverage. Together these close out **all remaining M1 admin API and admin UI work** that was tracked as an open PR chain.

**Current branch in progress, not yet a PR**: `feature/tenants-admin-ui` (stacked on `develop`) adds the `Tenants.razor` admin page (list + create) on top of the `TenantsController`/`ITenantProvisioningService` that landed with #47 — see the "Tenants admin UI" bullet below for what it found and fixed along the way (a real cross-tenant privilege-escalation bug in `TenantsController`, a stale-credentials-file gap, and a Blazor cross-component re-render gotcha).

**Milestone M1 is not yet fully closed** even once that branch merges — see "Next steps" below for what's genuinely still missing (self-service pages, registration/password-reset flows, rate limiting/CSP, Event Store UI). **No forward merge (`develop` → `docs` → `integration` → `main`) until M1 is complete.**

**TOTP + Push MFA** (merged, PR #33): RFC 6238 enrollment/verification with QR code and hashed single-use backup codes, plus push-mfa primitives (device registry, challenge lifecycle, ANH notifier). MFA login step via partial-authentication cookie (`Identity.Partial`) wired into the password page → `Account/Mfa` page. See `docs/mfa.md`.

### Implemented

Describes the current code, regardless of git merge state — everything through PR #47 is in `develop`; anything from `feature/tenants-admin-ui` (in progress, see "Current State" above) is called out explicitly below.

- **OAuth/OIDC engine** (OAuth project): `authorization_code` + PKCE S256, `client_credentials`, `refresh_token` (rotating), `device_code` (token processing); RS256 JWT with active key + N previous keys for validation during rotation.
- **OIDC consent** (PR #24): `Account/Consent` page wired into the authorize flow — shows requested scopes with client name → approve/deny (admin-approved scopes allowed without prompt).
- **Device code flow** (PR #25): `/connect/deviceauthorization` (device + user code issuance, RFC 8628 errors) + `/device` verification/approval UI; `ProcessDeviceCodeAsync` exchanges on poll.
- **Token introspection + revocation**: `/connect/introspect` (RFC 7662) and `/connect/revoke` (RFC 7009), both with `client_id`/`client_secret` authentication. Refresh tokens revoke the whole rotating family; access tokens go to a `RevokedToken` denylist (jti-hashed table) that is enforced by `/connect/userinfo` and introspection.
- **Server**: `ConnectController` (`/connect/token`, `/connect/deviceauthorization`, `/connect/introspect`, `/connect/revoke`, `/connect/jwks`, `/connect/userinfo`, `/connect/endsession`, `/.well-known/openid-configuration`), Razor Pages (login, consent, device approval, tenant selection, error), dynamic CORS (per-client origin whitelist), OpenAPI + Scalar + Swagger.
- **Admin API** (`/api/v1/*`, all `[Authorize(AuthenticationSchemes = "Bearer")]` + `[RequirePermission("identity-platform.{entity}.{action}")]`): `UsersController` (paged list, CRUD, lock/unlock, reset-password, role/permission/group assignment, effective-permissions), `DashboardController` (summary stats), `MeController` (`/me/tenants`), `RolesController`/`GroupsController` (tenant-scoped CRUD + role-permission / group-role assignment), `PermissionsController` (tenant-scoped catalog, `allTenants` flag), `ClientsController` (tenant-scoped CRUD, secret regeneration, activate/deactivate, full replace-on-update of grant types/scopes/redirect URIs/post-logout URIs/admin-consent scopes, cross-tenant platform-assignment check), `PlatformsController` (tenant-scoped CRUD), `AuditController` (tenant-scoped, paged, filterable by actor/action/target/date range), `TenantsController` (`GET`/`POST /api/v1/tenants`, both require `IsGlobalAdministratorAsync` on top of the permission check — see the dedicated bullet below for why that extra check is load-bearing). Every list/mutation endpoint takes an `allTenants` query flag, honored only for callers holding a global (`IsGlobal`) permission via `ITenantContext.IsGlobalAdministratorAsync`.
- **`Platform` vs `Client`**: not redundant — the relationship is one `Platform` to many `Client`s (`HasOne<Platform>().WithMany()` on `Client.PlatformId`). A `Platform` is the target *application* (e.g. "IdentityPlatform", "MobileApp") that one or more OAuth clients authenticate into (a confidential web client, a public PKCE client, a machine-to-machine client_credentials client — all separate `Client` registrations for the same underlying application). `Platform.PermissionMode` (`AuthOnly` vs `IdentityManaged`) is meant to record, per application, whether that application's *internal* roles/permissions are managed entirely by the application itself (`AuthOnly` — Identity is just doing authentication for it) or by this Identity platform (`IdentityManaged` — e.g. the seeded "IdentityAdmin" platform, whose roles/permissions/groups live here). **Not wired into any authorization logic yet** — currently pure metadata, set once by the admin UI/seeding and never read back by anything else. Documented here specifically so this distinction doesn't get lost before the behavior is actually implemented.
- **`ClientRepository.UpdateAsync`** needed a different fix than the PR #40 repositories: `Client`'s grant types/scopes/redirect URIs are EF `OwnsMany` owned collections, not independent join-entity types like `RolePermission`/`GroupRole` — reattaching a disconnected graph via `Update()` silently drops additions/removals for owned collections too (verified empirically), but the fix is to load the tracked instance and replay the diff through the entity's own `Add*`/`Remove*` methods, not to reconcile a `DbSet<TOwned>` directly.
- **Access-token `permission` claim** (previously dead code — `JwtService`/`ConnectController` had the transport plumbing but `TokenService` always passed `null`): `TokenService.GenerateTokensAsync` now calls `IPermissionEvaluator.GetEffectivePermissionsAsync(user.Id, tenantId)` and stamps one `"permission"` claim per name (not a single space-joined claim - `TokenIntrospectionService` reads it back via `FindAll`, which needs multiple claims of the same type to work). `client_credentials` tokens still carry no permissions (no real user). Introspection (`/connect/introspect`) now also returns `tid` for both access and refresh tokens, so a resource server can learn the token's tenant without decoding the JWT itself.
- **`JwtService.ValidateTokenAsync`** now sets `MapInboundClaims = false` on its `JwtSecurityTokenHandler`, matching the JWT bearer pipeline in `Program.cs`: the default `true` silently renames well-known short claim names ("tid", "sub") to Microsoft's Azure AD claim URIs, breaking any code reading them back by their original name. This is a separate internal re-validation path (introspection, revocation, `/connect/userinfo`) from the ASP.NET Core authentication pipeline, so it needed the same fix independently - found via `IntrospectionResponse.Tid` coming back null, then a second instance found in `/connect/userinfo`'s own `sub` claim read (fixed with a `Sub` claim fallback before `ClaimTypes.NameIdentifier`).
- **Multi-tenancy**: `Tenant`/`TenantMembership` entities; `Client`, `Group`, `Role`, `Permission`, `UserRole`, `UserPermission`, `AuditLog`, `UserConsent`, `RefreshToken`, `UserSession`, `AuthorizationCode`, `DeviceCode` all carry a non-nullable `TenantId`. Active tenant comes from the `tid` access-token claim via `ITenantContext`; switching tenants redoes the OIDC flow (`/login?tenant={slug}`), it's never a client-side variable flip.
- **Permissions are platform-scoped**: `Permission` gained a mandatory `PlatformId` (and `TenantId`, previously it had neither — it used to be a single global catalog). `Role` also gained a mandatory `PlatformId`. `RoleService.AssignPermissionAsync` enforces that a permission can only be attached to a role belonging to the *same* platform (`InvalidOperationException` → 403 otherwise) — a real enforcement constraint, not just informational tagging. This means the permission catalog is now **duplicated per tenant**: every tenant gets its own copy of the "IdentityAdmin" platform and its own ~32-row `identity-platform.*` permission catalog (rows share the same `Name` string across tenants but have distinct `Id`/`TenantId`/`PlatformId` — safe because `IPermissionEvaluator.HasPermissionAsync` matches by name, not by a single canonical `Permission.Id`). `Permission.IsGlobal` is orthogonal to this: it still means "grants visibility across tenants/platforms" (e.g. `identity-platform.tenant.create`), independent of which platform's catalog the permission itself belongs to. Provisioning this per-tenant catalog is what `TenantProvisioningService` (below) exists to do — hand-seeding it per tenant would have been unmaintainable.
- **`TenantProvisioningService`** (`Application/Tenants`): the only supported way to create a new tenant. `ProvisionAsync(name, slug)` creates the `Tenant`, an "IdentityAdmin" `Platform` (`PermissionMode.IdentityManaged`), the full permission catalog scoped to that platform, a `SuperAdmin` role with every permission, and a `SuperAdmins` group with that role — atomically, so a tenant is never left half-provisioned. `DbSeeder` now calls this for the bootstrap "Identity Tenant" instead of hand-seeding each piece separately; `TenantsController` (`POST /api/v1/tenants`, `identity-platform.tenant.create`, `IsGlobal`) exposes it over HTTP, and `Tenants.razor` (**`feature/tenants-admin-ui`**) is the admin UI for it.
- **`TenantsController` requires real global-admin status, not just the permission claim** (**`feature/tenants-admin-ui`**; found while building the Tenants UI): `tenant.read`/`tenant.create` are `IsGlobal` permissions, but they're duplicated into *every* tenant's own catalog like any other permission — so every tenant's own SuperAdmin holds them, and `[RequirePermission]` alone only checks "does this user have this permission in their current tenant", not "is this being exercised globally". Without an explicit `ITenantContext.IsGlobalAdministratorAsync` check in the controller (now added to both actions, `Forbid()` otherwise), any tenant's SuperAdmin could list or provision tenants system-wide. The admin UI mirrors this: `Tenants.razor`/`NavMenu.razor` gate on `TenantState.IsGlobalAdministrator` (server-confirmed via `/me/tenants`), not on `RequirePermission`/the client-decoded permission claim, since that claim is also true for any tenant's own SuperAdmin.
- **Cross-component live state in the WASM client**: `TenantState` (scoped service) is populated asynchronously by `MainLayout.OnAfterRenderAsync` calling `TenantState.LoadAsync()`, then `MainLayout` re-renders itself — but a *sibling* component like `NavMenu` is never told to re-render just because `MainLayout` was, so a nav item gated on `TenantState.IsGlobalAdministrator` stayed permanently hidden even after the flag flipped true. Fixed with a plain `event Action? Changed` on `TenantState`, raised at the end of `LoadAsync`, that `NavMenu` subscribes to in `OnInitialized` (unsubscribing in `Dispose`) and reacts to with its own `StateHasChanged`. Any future nav item gated on state loaded after first paint needs the same subscription, not just a `@if` on the shared service's property.
- **`superadmin-credentials.txt` only reflects the original seed password** (**`feature/tenants-admin-ui`**): `DbSeeder` writes it once, on first seed, and never again (`SeedUsersAsync` short-circuits once any user exists) — by design, this is a bootstrap snapshot, not a live mirror of the account's current password, matching how e.g. Django's `createsuperuser` works. If an admin resets the *system account's own* password later through `UsersController.ResetPassword`, that now re-syncs the file too (`SuperadminCredentialsWriter`, shared with `DbSeeder`) — gated strictly on `user.IsSystemAccount`, so a reset on any regular tenant user never touches the file (that would otherwise be a plaintext-password-log anti-pattern for arbitrary accounts). A reset done any other way (direct SQL, a future self-service flow) still won't update the file — the sync only wraps the one admin API path.
- **IdentityPlatform** is a Blazor **Server + WASM hybrid** (renamed from `AdminUi` in PR #34, `InteractiveAuto` render mode), split into two projects: `DgDevelopment.Identity.IdentityPlatform` (server host) and `DgDevelopment.Identity.IdentityPlatform.Client` (WASM interactive pages/layout). Pages: `Home` (dashboard), `Users`/`UserDetails` (role/permission/group assignment on the details page), `Roles`/`RoleDetails`, `Groups`/`GroupDetails`, `Clients`/`ClientDetails`, `Platforms`/`PlatformDetails`, `AuditLog`, `Tenants` (**`feature/tenants-admin-ui`**, list + create only, no delete), tenant switcher in `MainLayout`. Every admin page is wrapped in a `RequirePermission` component and hidden from `NavMenu` unless the caller's access token carries the matching `permission` claim - decoded client-side off the token (`JwtClaimsReader`, unverified, since every API call is still enforced server-side); this is UI-hiding only, not a security boundary. `Tenants`/its nav item are the one exception, gated on `TenantState.IsGlobalAdministrator` instead (see the dedicated bullet above for why). No dedicated page exists for browsing the `Permissions` catalog on its own — it's only ever shown as a dropdown inside Role/User assignment; low priority since it's read-only and small (~30 rows per tenant).
- **Client SDK** (`Client.Core` + `Client.Blazor`): `IdentityClient`, PKCE, token store in `sessionStorage`, refresh handler, `IdentityAuthStateProvider`, `SessionMarkerService` (`identity_marker` cookie) for SSR prerender restore. `IdentityAuthStateProvider.BuildPrincipal` also stamps `permission` claims from the access token (optional parameter, absent during SSR prerender - gated nav briefly hides until the WASM client goes interactive).
- **Test suite**: `DgDevelopment.Identity.Server.UnitTests` — 374 tests over domain, application, infrastructure, OAuth, and multi-tenant layers on a LocalDB fixture (`(localdb)\MSSQLLocalDB`, one throwaway database per test class, recreated every run). Coverage HTML auto-generated to `TestResults\html` on every Debug build (ReportGenerator 5.5.11). `DgDevelopment.Identity.IntegrationTests` — 10 tests driving the real IDP over `WebApplicationFactory<Program>` (login → password → consent → `/connect/token`, scripted with raw `HttpClient`, no Selenium/Aspire), on its own LocalDB database (also recreated every run, same LocalDB instance as unit tests but a different database, and a different SQL Server instance entirely from the real dev database on `localhost`); covers admin API CRUD, cross-tenant 403s, audit log visibility, `/connect/userinfo`, and CORS/auth pipeline end-to-end.
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

1. **Open the PR for `feature/tenants-admin-ui`** and merge it — nothing left to build, verified end-to-end in-browser (list, create, nav gating).
2. **No delete/deactivate for tenants**: `TenantsController` only has `GET`/`POST`; there's no way to remove a tenant once created, from the UI or the API. Low priority (tenants are meant to be long-lived), but worth knowing before someone creates a throwaway one expecting to clean it up.
3. **Permission-catalog drift for already-provisioned tenants**: `TenantProvisioningService.StandardPermissionCatalog` is the source of truth for a *new* tenant's permission set, but there's no reconciliation step for tenants provisioned *before* a permission was added to that list — found live when `identity-platform.tenant.create` (added alongside the tenant-scoping work) turned out to be missing from the original bootstrap "Identity Tenant"'s catalog, 403-ing its own SuperAdmin on tenant creation until the row was inserted by hand. The same will happen to every existing tenant, including this bootstrap one, the next time a permission is added to the catalog. Worth a small startup/admin reconciliation step (diff `StandardPermissionCatalog` against what a tenant actually has, insert what's missing) before this happens again silently.
5. **Self-service pages**: no `/profile`, `/logout`, `/mfa` (self-service device/backup-code management — login-time enrollment already works), or `/profile/emails`. Genuinely not started; a separate, more heterogeneous piece of work than the admin CRUD pages above (MFA self-service touches enrollment/backup-codes/push-device UI, not just forms over an API).
6. **Self-registration and password recovery**: no `/register`, no email verification, no "forgot password" flow — none of these endpoints exist at all (verified: no controller, no service method). Only an *admin-driven* password reset exists today (`UsersController.ResetPassword`, requires `identity-platform.user.update`). **`INotificationService`** (the transport layer any of this would need to send email) also doesn't exist — not even as an interface, nothing to swap out.
7. **Password policy**: `UserService.CreateAsync`/`ResetPasswordAsync` only reject null/whitespace — no length, complexity, or history rules.
8. **Lockout enforcement — already implemented**, contrary to what this file used to say: `UserAuthenticationService` locks an account for 15 minutes after 5 failed password attempts (`MaxFailedAttempts`), independent of the manual admin lock/unlock in the Users API. Nothing to do here.
9. **Event Store**: entity and repository (`IEventStoreRepository`) exist and are registered in DI, but nothing ever calls them — no service writes events, nothing reads them back, no API or UI. Fully dormant scaffolding, not just "missing UI" the way Audit Log was for a while.
10. **Rate limiting and CSP headers**: not implemented on any UI page or the login/token/userinfo endpoints.
11. **Docs pass**: a full documentation pass is still the planned final step before the M1 forward-merge, once everything above (or whatever subset is decided) is either done or explicitly deferred past M1.

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
