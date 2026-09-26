# Functional Specification — DgDevelopment.Identity

## 1. User Management

### 1.1 Registration and Profile

- User registration with username/email and password
- Email verification via confirmation link
- User profile with basic fields (first name, last name, date of birth, profile pictures)

> **Status (M1)**: registration and email verification implemented (`/register` - create a new tenant and become its owner, or join an existing one via `?tenant=slug`; `/account/verify-email`). Profile page (`/profile`) covers self-service password/email management; the richer profile fields listed above (first/last name, date of birth, profile pictures) are not yet modeled.

### 1.2 Email Aliases

- Each user has one primary email and N secondary email aliases
- Each email alias can be verified or unverified
- Login possible with any verified email associated to the user
- Ability to promote an alias to primary email
- Ability to remove aliases (cannot remove primary without having another)

### 1.3 Custom Claims

- Each user can have custom key-value claims
- Claims can be included in OAuth/OIDC tokens (if allowed by scope)
- Claims can be included in SAML assertions

## 2. Authentication

### 2.1 Local Authentication

- Login with username/email and password
- Configurable password policy (min length, complexity, expiration, history)
- Account lockout after N failed attempts
- Password reset via email

> **Status (M1)**: all implemented - `PasswordPolicy` (length, complexity, common-password blocklist), `PasswordExpirationPolicy` (90 days), `PasswordHistoryEntry`/`IPasswordHistoryService` (last 5 passwords), `UserAuthenticationService` (15-min lockout after 5 failed attempts), `/account/forgot-password` + `/account/reset-password`. Changing a password now also revokes every other session/refresh token, with a real-time push to any connected client - see CONTEXT.md's session-revocation bullet.

### 2.2 External Identity Providers (Milestone 3)

- Google, Microsoft, GitHub
- Linking/unlinking external accounts to existing user
- Optional automatic provisioning on first external login

### 2.3 Multi-Factor Authentication

#### TOTP (Time-based One-Time Password) — Milestone 1

- RFC 6238
- Enrollment via QR code + code verification
- Single-use backup codes generated at enrollment
- Ability to regenerate backup codes
- MFA configurable as mandatory per user/role

> **Status (M1)**: mandatory MFA is implemented for `GlobalAdmin`/`SuperAdmin` role holders specifically (per-tenant, 14-day grace period before enforcement), not as a general per-user/per-arbitrary-role toggle - see `docs/mfa.md`'s "Mandatory MFA for privileged roles" section.

#### Push Notification — Milestone 3

- Integration with Azure Notification Hubs
- Push notification sent to the user's authenticator app
- Approve/Deny directly from the notification
- Configurable request timeout

## 3. Authorization (RBAC + PBAC)

### 3.1 Permissions

- Atomic permission: string formatted `{resource}:{action}` (e.g. `user:read`, `invoice:approve`, `report:export`)
- CRUD permissions via admin API
- Permissions are global and shared

### 3.2 Roles

- A role is composed of one or more permissions (many-to-many)
- CRUD roles via admin API
- Role assignment to users and groups

### 3.3 Groups

- Groups with parent/child hierarchy (self-referencing)
- A user can belong to N groups
- A group can have N roles assigned
- Transitive permission inheritance:
  - Direct user permissions
  - Permissions via direct user roles
  - Permissions via direct group roles
  - Permissions via ancestor group roles (recursive transitive)

### 3.4 Entity Scoping

- Each permission/role assignment (to user or group) can have an optional Scope
- Scope defined as `ScopeType:ScopeValue` (e.g. `Department:Finance`, `Project:Alpha`)
- In JWT tokens: `user:read` (global) vs `user:read|department:finance` (scoped)
- The authorization endpoint evaluates both permission and scope

### 3.5 Effective Permissions Calculation

Runtime resolution algorithm:

1. Fetch direct `UserPermissions`
2. Fetch `UserRoles` -> `RolePermissions`
3. Fetch direct `UserGroups` -> `GroupRoles` -> `RolePermissions`
4. Walk up the group hierarchy (ParentGroupId) and fetch `GroupRoles` -> `RolePermissions` recursively
5. Merge all permissions, resolving scope conflicts (more specific scope wins)

## 4. Platform and Client

### 4.1 Platform

- A Platform represents a registered business application (e.g. "WebApp DgCommerce", "MobileApp DgCommerce")
- Each Platform has a `PermissionMode`:
  - **AuthOnly**: Identity provides only authentication and user profile. Permissions are managed by the client application itself.
  - **IdentityManaged**: Identity provides authentication, user profile AND permissions. JWT tokens include `permission` claims.

### 4.2 OAuth Client

- Each Platform has one or more OAuth Clients (e.g. web client, mobile client, desktop client)
- A Client is the technical OAuth registration with `client_id`, `client_secret`, `redirect_uris`, `grant_types`, `scopes`
- Clients can be `confidential` or `public` (per OAuth 2.0)

## 5. Authentication Protocols

### 5.1 OAuth 2.0 / OpenID Connect (Milestone 1)

#### Endpoints

| Endpoint | Description |
|---|---|
| `/.well-known/openid-configuration` | Discovery document |
| `/connect/authorize` | Authorization endpoint |
| `/connect/token` | Token endpoint |
| `/connect/userinfo` | UserInfo endpoint |
| `/connect/endsession` | End session / logout |
| `/connect/jwks` | JSON Web Key Set |
| `/connect/deviceauthorization` | Device authorization endpoint |
| `/connect/introspect` | Token introspection |
| `/connect/revoke` | Token revocation |

#### Supported Grant Types

- **authorization_code** + PKCE (S256)
- **client_credentials** (server-to-server)
- **refresh_token** (with rotating refresh token policy)
- **device_code** (for input-constrained devices)

#### Security Features

- **PAR** (Pushed Authorization Request, RFC 9126): authorization request pushed before redirect, avoiding URL parameters
- **JAR** (JWT-Secured Authorization Request, RFC 9101): request is JWT-signed
- **DPoP** (Demonstration of Proof-of-Possession, RFC 9449): token binding to client
- **mTLS** (RFC 8705): client authentication via TLS certificate
- **Token Binding**: association of token to TLS channel

#### Tokens

- **ID Token**: signed JWT (RS256/ES256), contains `sub`, `iss`, `aud`, `exp`, `iat`, `auth_time`, `nonce`
- **Access Token**: signed JWT or reference token (opaque), contains `sub`, `client_id`, `scope`, `permission` (if IdentityManaged)
- **Refresh Token**: opaque, rotating (each use generates a new refresh token, the old one is revoked)
- **Device Code**: opaque, exchangeable for tokens after user authorization

#### Consent UI

- Consent page for user when a client requests access to scopes
- User can approve/deny
- Consents can be remembered (persistent consent)

### 5.2 SAML 2.0 (Milestone 2)

#### Endpoints

| Endpoint | Role | Description |
|---|---|---|
| `/saml/metadata` | IdP Metadata | Identity Provider metadata |
| `/saml/sso` | SSO | Single Sign-On (HTTP-Redirect and HTTP-POST) |
| `/saml/slo` | SLO | Single Logout (HTTP-Redirect and HTTP-POST) |
| `/saml/artifact` | Artifact Resolution | SAML artifact resolution (SOAP) |

#### Features

- Signed SAML assertion generation and validation (XML DSig)
- Supported bindings: HTTP-Redirect, HTTP-POST, Artifact (SOAP)
- X.509 certificate management for signing and encryption
- Service Provider metadata ingestion and validation
- Attribute mapping: user claims -> SAML attributes
- Session Index for session tracking and SLO

## 6. Audit and Event Store

### 6.1 Audit Log

Every sensitive operation is traced:

- **Actor**: who performed the action (userId, clientId)
- **Action**: what was done (e.g. `user.created`, `token.issued`, `permission.granted`)
- **Target**: on which resource (e.g. target userId, target clientId)
- **Details**: JSON payload with operation details
- **Timestamp**: when it occurred
- **Outcome**: success/failure

### 6.2 Event Store

For significant domain operations, an event is emitted:

- Lightweight event sourcing: every relevant state change produces an event
- Events are immutable and append-only
- Used for audit trail and future projections

> **Status**: scaffolded early on, then **removed** during the M1 close-out - zero consumers ever used it, and the Audit Log (6.1) already covers what it would have offered except a per-aggregate version counter, which only matters for actual replay/projection consumers. Kept here as a requirement to revisit if a real consumer (a webhook, an external integration) shows up.

### 6.3 Traced Operations

- User creation/modification/deletion
- Login/logout
- Token issuance (authorization code, access token, refresh token)
- User consent
- Password change
- MFA enrollment/removal
- Role, permission, group modifications
- Client and platform registration/modification
- Authentication/authorization errors

## 7. API

### 7.1 Public APIs

| Area | Base Path | Protection |
|---|---|---|
| OAuth/OIDC | `/connect/*` | Public (with OAuth validation) |
| SAML | `/saml/*` | Public (with SAML validation) |
| UserInfo | `/connect/userinfo` | Access Token |

### 7.2 Admin APIs (protected)

| Area | Base Path | Description |
|---|---|---|
| Users | `/api/v1/users` | CRUD users |
| Roles | `/api/v1/roles` | CRUD roles |
| Permissions | `/api/v1/permissions` | CRUD permissions |
| Groups | `/api/v1/groups` | CRUD groups |
| Clients | `/api/v1/clients` | CRUD OAuth clients |
| Platforms | `/api/v1/platforms` | CRUD platforms |
| Audit | `/api/v1/audit` | Audit log querying |

### 7.3 UI

- Login page (`/login`)
- Logout page (`/logout`)
- Consent page (`/consent`)
- User profile page (`/profile`)
- Error page (`/error`)
- MFA management page (`/mfa`)
- Email alias management page (`/profile/emails`)

## 8. Client SDK

### 8.1 .NET Core (DgDevelopment.Identity.Client.Core) — Milestone 1

- `IdentityClient` with support for all grant types
- Automatic token storage and refresh
- `DelegatingHandler` for HttpClient with token injection
- UserInfo retrieval

### 8.2 Blazor — Milestone 1

- `IdentityAuthStateProvider` (extends `AuthenticationStateProvider`)
- Pre-built Razor components (login button, user menu)
- Automatic refresh token handling

### 8.3 React — Milestone 3

- npm package `@dgdevelopment/identity-client`
- React Context for auth state
- Hooks: `useAuth()`, `useUser()`, `usePermissions()`
- Components: `<LoginButton>`, `<ProtectedRoute>`

### 8.4 WPF — Milestone 3

- Auth code flow + PKCE via system browser / WebView2
- Secure token storage (DPAPI)
- `IdentityHttpMessageHandler` for HttpClient

### 8.5 MAUI — Milestone 3

> **Status**: first cut implemented (`DgDevelopment.Identity.Client.Maui`, targets Android and Windows - iOS/MacCatalyst need a Mac toolchain to build past the C# entry point), built opportunistically during M1 rather than waiting for Milestone 3. See `docs/client-sdk.md`.

- Web authenticator browser for login
- SecureStorage for tokens
- MAUI dependency injection integration

## 9. Localization Integration (Milestone 4)

### 9.1 Identity -> Localization

- Identity calls the Localization service to fetch translated labels
- Scopes: login pages, confirmation emails, error messages, consent UI
- Local cache with configurable TTL

### 9.2 Localization -> Identity

- Localization calls Identity to validate tokens and permissions
- Introspection endpoint for access token verification
- Permissions endpoint for authorization checks
