# DgDevelopment Identity

Complete Identity Provider for the DgDevelopment ecosystem.

Provides authentication, authorization, user profile management and multi-client integration with support for modern protocols, MFA and push notifications.

## Key Features

- **Identity Server** — OAuth 2.0 / OpenID Connect and SAML 2.0 (custom engine)
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
| .NET Aspire | 10.0 |
| Entity Framework Core | 10.0 |
| SQL Server | >= 2022 |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- SQL Server (external instance for local development; Azure SQL for higher environments)
- Docker Desktop (for Aspire Dashboard and support containers)

## Quick Start

```bash
# Clone the repository
git clone https://github.com/dgastaldo/DgDevelopment.Identity.git
cd DgDevelopment.Identity

# Configure database connection in appsettings.Development.json
# (src/DgDevelopment.Identity.Server/appsettings.Development.json)

# Run with Aspire
aspire run
```

## Solution Structure

```
DgDevelopment.Identity/
├── src/
│   ├── DgDevelopment.Identity.AppHost/          # .NET Aspire orchestration host
│   ├── DgDevelopment.Identity.ServiceDefaults/   # Service Discovery, HTTP Resilience
│   ├── DgDevelopment.Identity.Domain/            # Entities, Value Objects, interfaces
│   ├── DgDevelopment.Identity.Application/       # Use cases, CQRS handlers, DTOs
│   ├── DgDevelopment.Identity.Infrastructure/    # EF Core, SQL Server, external services
│   ├── DgDevelopment.Identity.Server/            # ASP.NET Core host (API + UI)
│   ├── DgDevelopment.Identity.OAuth/             # Custom OAuth 2.0 / OIDC engine
│   └── DgDevelopment.Identity.Saml/              # Custom SAML 2.0 engine
├── clients/
│   ├── DgDevelopment.Identity.Client.Core/       # Base .NET SDK
│   ├── DgDevelopment.Identity.Client.Blazor/     # Blazor integration
│   ├── DgDevelopment.Identity.Client.Wpf/        # WPF integration
│   ├── DgDevelopment.Identity.Client.Maui/       # .NET MAUI integration
│   └── DgDevelopment.Identity.Client.React/      # TypeScript SDK (npm package)
├── tests/
│   ├── DgDevelopment.Identity.UnitTests/
│   └── DgDevelopment.Identity.IntegrationTests/
└── docs/
    ├── functional-specification.md
    └── architecture.md
```

## Documentation

- [Functional Specification](docs/functional-specification.md)
- [Architecture Document](docs/architecture.md)
- [Branching Strategy](BRANCHING.md)
- [CONTEXT.md](CONTEXT.md) — Context for AI tools

## Milestones

| Milestone | Contents |
|---|---|
| **M1** | Foundation + OAuth/OIDC engine + TOTP + .NET Core & Blazor SDK |
| **M2** | SAML 2.0 custom IdP |
| **M3** | Client SDK (React, WPF, MAUI) + Push MFA + External Providers |
| **M4** | Bidirectional integration with Localization Service |
