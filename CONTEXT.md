# CONTEXT — DgDevelopment.Identity

This file provides full project context for AI tools and LLMs operating on the repository.

## Overview

**DgDevelopment.Identity** is a complete Identity Provider for the DgDevelopment ecosystem. It provides authentication (OAuth 2.0 / OIDC, SAML 2.0), authorization (RBAC + PBAC with permissions, roles, hierarchical groups), user profile management, MFA and multi-client SDKs.

## Current State — 2026-08-16

The OAuth 2.0 / OIDC authentication cycle is **fully merged into `develop`** (PRs #1–#23, the whole stack closed). All feature/fix branches are deleted; only `develop`, `docs`, `integration`, `main` remain (local + remote).

**Milestone M1 is still open**: the OAuth/OIDC foundation, login flow and client SDK are functional, but the remaining M1 features (consent, device flow, TOTP, admin APIs, RBAC, audit, tests) are not implemented. **No forward merge (`develop` → `docs` → `integration` → `main`) until M1 is complete.**

### Implemented (merged)

- **OAuth/OIDC engine** (OAuth project): `authorization_code` + PKCE S256, `client_credentials`, `refresh_token` (rotating), `device_code` (token processing); RS256 JWT with active key + N previous keys for validation during rotation.
- **Server**: `ConnectController` (`/connect/token`, `/connect/jwks`, `/connect/userinfo`, `/connect/endsession`, `/.well-known/openid-configuration`), Razor Pages (login, consent stub, error), dynamic CORS (per-client origin whitelist), OpenAPI + Scalar + Swagger.
- **AdminUi** is a Blazor **Server + WASM hybrid**, split into two projects: `DgDevelopment.Identity.AdminUi` (server host) and `DgDevelopment.Identity.AdminUi.Client` (WASM interactive pages/layout).
- **Client SDK** (`Client.Core` + `Client.Blazor`): `IdentityClient`, PKCE, token store in `sessionStorage`, refresh handler, `IdentityAuthStateProvider`, `SessionMarkerService` (`identity_marker` cookie) for SSR prerender restore.
- **DB seeding**: 30 permissions, SuperAdmin role, SuperAdmins group, superadmin user, admin client. Stable credentials between runs.

### Working end-to-end flow

- AdminUi `/login` → IDP `/connect/authorize` (PKCE S256) → IDP login (static SSR Razor Page) → authorization code → AdminUi `/callback` → `/connect/token` → tokens saved in `sessionStorage` + `identity_marker` cookie
- Ports are **dynamic** (Aspire binding); `IdentityBaseUrl` / `AdminBaseUrl` / `Identity:AdminClientSecret` come from AppHost/user-secrets
- AdminUi top bar shows avatar (initials) + username + Logout when authenticated; Home nav is visible only when authenticated
- Session restore: `IdentityAuthStateProvider` (WASM) reads stored tokens, refreshes if expired, fetches `/connect/userinfo` once (cached); `ServerIdentityAuthStateProvider` (prerender) reads the `identity_marker` cookie without network calls
- IDP `/connect/userinfo` returns `sub` / `name` / `email`; client DTOs (`TokenResponse`, `UserInfo`) are aligned to the IDP **snake_case** responses

### Development database

- Migrations run **up-only** on every start (`MigrateAsync`); `EnsureDeletedAsync` was removed
- Seeding runs **once** (first run): superadmin password and admin client secret are **stable** between runs
- `superadmin-credentials.txt` / `admin-client-credentials.txt` are written only on the first seed
- Stale IDP session cookies (user no longer in the DB after a reseed) self-heal: IDP signs out and shows the login form instead of an `invalid_user` error

### Next steps / open work (M1 completion)

Ordered by dependency:

1. **OIDC consent screen** (IDP): wire the existing `Account/Consent` stub into the authorize flow — show requested scopes → approve/deny → issue the code. Branch `feature/oauth-consent`.
2. **Device code flow**: `ProcessDeviceCodeAsync` is implemented in `TokenService`, but the `/connect/deviceauthorization` endpoint (device + user code issuance) and the verification/approval UI are missing — the endpoint is only advertised in the discovery document.
3. **`/connect/introspect` + `/connect/revoke`**: advertised in discovery but not implemented.
4. **End-to-end verification**: first/all-run smoke test of `/connect/token` (keep the AppHost `Identity:AdminClientSecret` user-secret in sync with the seeded value) and of the AdminUi session restore via `userinfo`.
5. **TOTP MFA** (RFC 6238): domain model (`TotpSecret`, `BackupCode`) and table exist; no enrollment/verification flow yet.
6. **Admin API `/api/v1/*`**: users, roles, permissions, groups, clients, platforms, audit — none implemented (only `ConnectController` exists).
7. **RBAC/PBAC**: effective-permissions algorithm (documented), entity scoping, `permission` claims in access tokens.
8. **User management**: registration + email verification, password reset, password policy, lockout enforcement.
9. **Audit Log + Event Store**: entities exist; no implementation.
10. **UI pages**: `/profile`, `/logout`, `/mfa`, `/profile/emails`; rate limiting (login/token/userinfo) and CSP headers on UI pages.
11. **Tests**: `UnitTests` and `IntegrationTests` projects exist but contain no test source files — coverage is written as the features land.

After M1 is complete: **forward merge** `develop` → `docs` → `integration` → `main`, then milestones M2–M4 (SAML 2.0, React/WPF/MAUI client SDKs, Push MFA + external providers, localization integration).

## Architecture Rules

### Clean Architecture — strictly controlled dependencies

```
AdminUi ──> Client.Blazor ──> Client.Core
AdminUi.Client ──> Client.Blazor ──> Client.Core
Server ──> Application ──> Domain <── Infrastructure (implements Domain interfaces)
  │            ▲               ▲
  ├──> OAuth ──┘               │
  └──> Saml ───────────────────┘
        OAuth ──> Application ──> Domain
        Saml ──> Application ──> Domain
AppHost ──> Server, AdminUi
```

- **Domain** references no other projects. Contains entities, value objects, repository interfaces, domain services.
- **Application** references only Domain. Contains use cases, DTOs, application service interfaces.
- **Infrastructure** references Domain (to implement interfaces). Contains EF Core, concrete repositories, external services, security (Argon2 hasher).
- **Server** references Application, Infrastructure, OAuth, Saml, ServiceDefaults. Contains API controllers, Razor Pages (login/consent UI), host configuration, seeding.
- **OAuth** and **Saml** reference Application and Domain. Contain pure protocol logic.
- **Client.Core** has no project references. Contains OIDC client logic, DTOs.
- **Client.Blazor** references Client.Core. Contains Blazor auth components.
- **AdminUi** references Client.Blazor. Blazor Server host (prerender, `ServerIdentityAuthStateProvider`).
- **AdminUi.Client** references Client.Blazor. Blazor WASM interactive pages/layout (login, callback, home, nav).

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
