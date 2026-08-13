# CONTEXT — DgDevelopment.Identity

This file provides full project context for AI tools and LLMs operating on the repository.

## Overview

**DgDevelopment.Identity** is a complete Identity Provider for the DgDevelopment ecosystem. It provides authentication (OAuth 2.0 / OIDC, SAML 2.0), authorization (RBAC + PBAC with permissions, roles, hierarchical groups), user profile management, MFA and multi-client SDKs.

## Active Stack

The project uses stacked feature branches. Current active chain:

```
feature/oauth-server ──> develop
      ▲
feature/oauth-hardening
      ▲
feature/client-blazor-sdk
      ▲
feature/oauth-connect
      ▲
feature/docs-update (current)
```

## Architecture Rules

### Clean Architecture — strictly controlled dependencies

```
AdminUi ──> Client.Blazor ──> Client.Core
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
- **AdminUi** references Client.Blazor. Blazor Hybrid admin dashboard.

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
