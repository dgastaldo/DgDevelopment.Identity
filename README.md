# DgDevelopment Identity

Complete Identity Provider for the DgDevelopment ecosystem.

Provides authentication, authorization, user profile management and multi-client integration with support for modern protocols, MFA and push notifications.

## Key Features

- **Identity Server** — OAuth 2.0 / OpenID Connect (custom engine), SAML 2.0 (planned)
- **User Management** — CRUD users, profiles, multiple email aliases, custom claims
- **Authorization (RBAC + PBAC)** — Atomic permissions, composite roles, hierarchical groups with transitive inheritance, entity scoping
- **Multi-Factor Authentication** — TOTP (RFC 6238), push notifications via Azure Notification Hubs
- **Platform & Multi-Client** — Apps registered as Platforms, AuthOnly or IdentityManaged mode, SDKs for Blazor, React, WPF, MAUI
- **Audit & Event Store** — Full operation tracing
- **Localization Integration** — Bidirectional with the localization service (Milestone 4)

## Tech Stack

| Technology | Version |
|---|---|
| .NET (SDK) | 10.0 |
| ASP.NET Core | 10.0 |
| .NET Aspire | 13.4 |
| Entity Framework Core | 10.0 |
| SQL Server | >= 2022 |
| OpenAPI | Microsoft.AspNetCore.OpenApi + Scalar + Swagger |
| Password Hashing | Argon2id (Konscious) |
| JWT | Microsoft.IdentityModel.Tokens 8.22 |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- SQL Server (external instance for local development; Azure SQL for higher environments)
- Docker Desktop (for Aspire Dashboard and support containers)

## Quick Start

```bash
# Clone the repository
git clone https://github.com/dgastaldo/DgDevelopment.Identity.git
cd DgDevelopment.Identity

# Configure database connection string as a user secret
dotnet user-secrets set "ConnectionStrings:IdentityDb" "Server=localhost;Database=DgDevelopmentIdentity;Trusted_Connection=True;TrustServerCertificate=True" --project src/DgDevelopment.Identity.AppHost

# Run with Aspire
aspire run
```

On first run, the seeder creates:
- `superadmin-credentials.txt` — superadmin username and password
- `admin-client-credentials.txt` — admin client ID and secret

> These files are `.gitignore`d and should be stored securely.

## Solution Structure

```
DgDevelopment.Identity/
├── src/
│   ├── DgDevelopment.Identity.AppHost/          # .NET Aspire orchestration host
│   ├── DgDevelopment.Identity.ServiceDefaults/   # Service Discovery, HTTP Resilience
│   ├── DgDevelopment.Identity.Domain/            # Entities, Value Objects, interfaces
│   ├── DgDevelopment.Identity.Application/       # Use cases, DTOs
│   ├── DgDevelopment.Identity.Infrastructure/    # EF Core, SQL Server, repositories
│   ├── DgDevelopment.Identity.Server/            # ASP.NET Core host (API + Razor Pages)
│   ├── DgDevelopment.Identity.OAuth/             # Custom OAuth 2.0 / OIDC engine
│   ├── DgDevelopment.Identity.Saml/              # Custom SAML 2.0 engine (planned)
│   └── DgDevelopment.Identity.AdminUi/           # Blazor Hybrid admin dashboard
├── clients/
│   ├── DgDevelopment.Identity.Client.Core/       # Base .NET SDK (OIDC client)
│   ├── DgDevelopment.Identity.Client.Blazor/     # Blazor auth components + DI
│   ├── DgDevelopment.Identity.Client.Wpf/        # WPF integration (planned)
│   ├── DgDevelopment.Identity.Client.Maui/       # .NET MAUI integration (planned)
│   └── DgDevelopment.Identity.Client.React/      # TypeScript SDK (planned)
├── tests/
│   ├── DgDevelopment.Identity.UnitTests/
│   └── DgDevelopment.Identity.IntegrationTests/
└── docs/
    ├── functional-specification.md
    ├── architecture.md
    └── client-sdk.md
```

## Documentation

- [Functional Specification](docs/functional-specification.md)
- [Architecture Document](docs/architecture.md)
- [Client SDK](docs/client-sdk.md)
- [Branching Strategy](BRANCHING.md)
- [CONTEXT.md](CONTEXT.md) — Context for AI tools

## Milestones

| Milestone | Status | Contents |
|---|---|---|
| **M1** | 🔄 In progress | Foundation + OAuth/OIDC engine + TOTP + .NET Core & Blazor SDK |
| **M2** | ⬜ Planned | SAML 2.0 custom IdP |
| **M3** | ⬜ Planned | Client SDK (React, WPF, MAUI) + Push MFA + External Providers |
| **M4** | ⬜ Planned | Bidirectional integration with Localization Service |

### M1 Progress

| Feature | Status |
|---|---|
| Domain entities + value objects + repository interfaces | ✅ Done |
| EF Core Infrastructure (DbContext, configurations, repositories) | ✅ Done |
| OAuth/OIDC Engine (6 services: key, JWT, client, authorize, token, user interaction) | ✅ Done |
| Server integration (ConnectController, Razor Pages, DI) | ✅ Done |
| AdminUi Blazor Hybrid (navbar, footer, login flow) | ✅ Done |
| Password hashing (Argon2id) + authentication service | ✅ Done |
| DB seeding (30 permissions, SuperAdmin, SuperAdmins, superadmin user, admin client) | ✅ Done |
| API documentation (OpenAPI + Scalar + Swagger) | ✅ Done |
| Client SDK (.NET Core + Blazor) | ✅ Done |
| TOTP MFA | ⬜ Planned |
