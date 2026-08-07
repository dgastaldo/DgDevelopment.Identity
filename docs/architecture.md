# Architecture Document — DgDevelopment.Identity

## 1. High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    .NET Aspire AppHost                       │
│                                                              │
│  ┌──────────────────────┐    ┌──────────────────────────┐   │
│  │   Identity Server     │    │     SQL Server            │   │
│  │   (ASP.NET Core)      │───>│     (external instance)   │   │
│  │                       │    │                           │   │
│  │  ┌─────────────────┐ │    └──────────────────────────┘   │
│  │  │ OAuth Engine     │ │                                    │
│  │  ├─────────────────┤ │    ┌──────────────────────────┐   │
│  │  │ SAML Engine      │ │    │  Azure Notification Hubs │   │
│  │  ├─────────────────┤ │    │  (M3)                     │   │
│  │  │ TOTP MFA        │ │───>│                           │   │
│  │  ├─────────────────┤ │    └──────────────────────────┘   │
│  │  │ Admin API        │ │                                    │
│  │  ├─────────────────┤ │    ┌──────────────────────────┐   │
│  │  │ Login/Consent UI │ │    │  Localization Service    │   │
│  │  └─────────────────┘ │    │  (M4)                     │   │
│  └──────────────────────┘    └──────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

## 2. Clean Architecture (Internal)

```
┌─────────────────────────────────────────────┐
│  Server (ASP.NET Core Host)                  │
│  - Controllers (API)                         │
│  - Razor Pages (Login, Consent, Profile UI)  │
│  - DI Composition Root                       │
└──────────┬──────────────────────────────────┘
           │ depends on
┌──────────▼──────────┐  ┌────────────────────┐
│  Application Layer   │  │  OAuth / Saml      │
│  - Use Cases         │  │  - Protocol logic  │
│  - CQRS Handlers     │  │  - Token service   │
│  - DTOs              │  │  - Crypto service  │
└──────────┬──────────┘  └──────┬─────────────┘
           │ depends on         │ depends on
┌──────────▼────────────────────▼──────────────┐
│  Domain Layer                                 │
│  - Entities (User, Client, Permission, etc.)  │
│  - Value Objects (EmailAddress, TokenId)      │
│  - Repository Interfaces                      │
│  - Domain Events                              │
└──────────────────────▲───────────────────────┘
                       │ implements
┌──────────────────────┴───────────────────────┐
│  Infrastructure Layer                         │
│  - EF Core DbContext                          │
│  - Repository Implementations                 │
│  - SQL Server Provider                        │
│  - Azure Notification Hubs Client             │
│  - JWT/SAML Crypto Providers                  │
└──────────────────────────────────────────────┘
```

Dependency rule: dependencies point inward. Outer layers depend on inner layers. Domain has zero external dependencies.

## 3. Domain Model

### 3.1 Core Entities

```
User (Aggregate Root)
├── Id: Guid
├── Username: string
├── PasswordHash: string
├── IsActive: bool
├── IsLocked: bool
├── LockoutEnd: DateTime?
├── FailedLoginAttempts: int
├── CreatedAt: DateTime
├── UpdatedAt: DateTime
│
├── UserEmails: List<UserEmail>
│   ├── Email: string
│   ├── IsPrimary: bool
│   ├── IsVerified: bool
│   └── VerifiedAt: DateTime?
│
├── UserClaims: List<UserClaim>
│   ├── Type: string
│   └── Value: string
│
├── UserLogins: List<UserLogin>
│   ├── Provider: string
│   ├── ProviderKey: string
│   └── ProviderDisplayName: string
│
├── UserRoles: List<UserRole>
│   ├── RoleId: Guid
│   └── ScopeType? / ScopeValue?
│
├── UserPermissions: List<UserPermission>
│   ├── PermissionId: Guid
│   └── ScopeType? / ScopeValue?
│
└── UserGroups: List<UserGroup>
    └── GroupId: Guid

Role
├── Id: Guid
├── Name: string
├── Description: string
└── RolePermissions: List<RolePermission>
    ├── PermissionId: Guid
    └── ScopeType? / ScopeValue?

Permission
├── Id: Guid
├── Name: string (e.g. "user:read")
├── Description: string
└── ResourceType: string

Group (self-referencing hierarchy)
├── Id: Guid
├── Name: string
├── Description: string
├── ParentGroupId: Guid?
│
├── Parent: Group?
├── Children: List<Group>
├── UserGroups: List<UserGroup>
└── GroupRoles: List<GroupRole>
    ├── RoleId: Guid
    └── ScopeType? / ScopeValue?

Platform
├── Id: Guid
├── Name: string
├── Description: string
├── PermissionMode: AuthOnly | IdentityManaged
└── Clients: List<Client>

Client
├── Id: Guid
├── ClientId: string
├── ClientSecret: string (hashed)
├── PlatformId: Guid?
├── Name: string
├── ClientType: Confidential | Public
├── RequirePkce: bool
├── RequireConsent: bool
├── AllowedGrantTypes: List<ClientGrantType>
├── AllowedScopes: List<ClientScope>
└── RedirectUris: List<ClientRedirectUri>

TotpSecret
├── UserId: Guid
├── SecretKey: string (encrypted)
├── IsEnabled: bool
├── EnabledAt: DateTime?
└── BackupCodes: List<BackupCode>
    ├── Code: string (hashed)
    └── IsUsed: bool

UserSession
├── Id: Guid
├── UserId: Guid
├── SessionId: string
├── CreatedAt: DateTime
├── ExpiresAt: DateTime
└── IsRevoked: bool
```

### 3.2 Token Entities

```
AuthorizationCode
├── Id: Guid
├── Code: string (hashed)
├── ClientId: Guid
├── UserId: Guid
├── RedirectUri: string
├── Scopes: string
├── CodeChallenge: string?
├── CodeChallengeMethod: string?
├── IsUsed: bool
├── CreatedAt: DateTime
└── ExpiresAt: DateTime

RefreshToken
├── Id: Guid
├── Token: string (hashed)
├── ClientId: Guid
├── UserId: Guid
├── Scopes: string
├── PreviousTokenId: Guid?
├── IsRevoked: bool
├── CreatedAt: DateTime
└── ExpiresAt: DateTime

DeviceCode
├── Id: Guid
├── DeviceCode: string (hashed)
├── UserCode: string (hashed)
├── ClientId: Guid
├── UserId: Guid?
├── Scopes: string
├── IsAuthorized: bool
├── IsUsed: bool
├── CreatedAt: DateTime
└── ExpiresAt: DateTime
```

### 3.3 Audit & Event Store

```
AuditLog
├── Id: Guid
├── ActorId: Guid?
├── ActorType: string (User/Client)
├── Action: string
├── TargetId: string?
├── TargetType: string?
├── Details: string (JSON)
├── Outcome: Success | Failure
├── IpAddress: string?
├── UserAgent: string?
└── Timestamp: DateTime

Event (Event Store)
├── Id: Guid
├── AggregateId: Guid
├── AggregateType: string
├── EventType: string
├── Data: string (JSON)
├── Version: int
└── Timestamp: DateTime
```

## 4. OAuth 2.0 / OIDC Flows

### 4.1 Authorization Code + PKCE

```
Client (SPA/Mobile)                Identity Server            User (Browser)
      │                                  │                         │
      │  1. PAR: POST /connect/authorize │                         │
      │  (with code_challenge)           │                         │
      │─────────────────────────────────>│                         │
      │                                  │                         │
      │  2. 201 Created (request_uri)    │                         │
      │<─────────────────────────────────│                         │
      │                                  │                         │
      │  3. Redirect to authorize        │                         │
      │  (with request_uri)              │                         │
      │─────────────────────────────────>│                         │
      │                                  │  4. Login page          │
      │                                  │────────────────────────>│
      │                                  │                         │
      │                                  │  5. Credentials         │
      │                                  │<────────────────────────│
      │                                  │                         │
      │                                  │  6. Consent page        │
      │                                  │────────────────────────>│
      │                                  │                         │
      │                                  │  7. Approve             │
      │                                  │<────────────────────────│
      │                                  │                         │
      │  8. Redirect with auth code      │                         │
      │<─────────────────────────────────│────────────────────>    │
      │                                  │                         │
      │  9. POST /connect/token          │                         │
      │  (code + code_verifier)          │                         │
      │─────────────────────────────────>│                         │
      │                                  │                         │
      │  10. Token Response              │                         │
      │  (access_token + id_token       │                         │
      │   + refresh_token)              │                         │
      │<─────────────────────────────────│                         │
```

### 4.2 Device Code Flow

```
Device Client                     Identity Server            User (Browser)
      │                                  │                         │
      │  1. POST /connect/deviceauthorize│                         │
      │─────────────────────────────────>│                         │
      │                                  │                         │
      │  2. device_code + user_code      │                         │
      │     + verification_uri           │                         │
      │<─────────────────────────────────│                         │
      │                                  │                         │
      │                                  │  3. Enter user_code      │
      │                                  │<────────────────────────│
      │                                  │                         │
      │                                  │  4. Approve device      │
      │                                  │────────────────────────>│
      │                                  │                         │
      │  5. Poll POST /connect/token     │                         │
      │  (grant_type=device_code)        │                         │
      │─────────────────────────────────>│                         │
      │                                  │                         │
      │  6. Token Response               │                         │
      │<─────────────────────────────────│                         │
```

## 5. SAML 2.0 SSO Flow

```
Service Provider (SP)            Identity Provider (IdP)         User (Browser)
      │                                  │                              │
      │  1. User accesses protected     │                              │
      │     resource on SP               │                              │
      │                                  │                              │
      │  2. SP generates AuthnRequest    │                              │
      │─────────────────────────────────>│                              │
      │                                  │                              │
      │                                  │  3. Login page               │
      │                                  │─────────────────────────────>│
      │                                  │                              │
      │                                  │  4. Credentials              │
      │                                  │<─────────────────────────────│
      │                                  │                              │
      │  5. IdP sends SAML Response       │                              │
      │     (Assertion) via HTTP-POST    │                              │
      │<─────────────────────────────────│─────────────────────────────>│
      │                                  │                              │
      │  6. SP validates assertion       │                              │
      │     User is authenticated        │                              │
```

## 6. JWT Token Structure

### 6.1 ID Token

```json
{
  "iss": "https://identity.dgdevelopment.local",
  "sub": "user-id-guid",
  "aud": "client-id",
  "exp": 1234567890,
  "iat": 1234567890,
  "auth_time": 1234567880,
  "nonce": "random-nonce-value",
  "amr": ["pwd", "totp"],
  "email": "user@example.com",
  "email_verified": true,
  "name": "John Doe"
}
```

### 6.2 Access Token (IdentityManaged mode)

```json
{
  "iss": "https://identity.dgdevelopment.local",
  "sub": "user-id-guid",
  "client_id": "client-id",
  "scope": "openid profile email api",
  "permission": [
    "user:read",
    "invoice:read|department:finance",
    "report:export"
  ],
  "exp": 1234567890,
  "iat": 1234567860,
  "jti": "unique-token-id"
}
```

## 7. Effective Permissions Algorithm

```
GetEffectivePermissions(userId):
  permissions = []

  // 1. Direct permissions
  permissions += UserPermissions
    .Where(up => up.UserId == userId)
    .Select(up => (up.Permission.Name, up.ScopeType, up.ScopeValue))

  // 2. Permissions via direct roles
  permissions += UserRoles
    .Where(ur => ur.UserId == userId)
    .Join(RolePermissions, ur => ur.RoleId, rp => rp.RoleId)
    .Select(rp => (rp.Permission.Name, ur.ScopeType, ur.ScopeValue))

  // 3. Permissions via direct groups
  directGroupIds = UserGroups
    .Where(ug => ug.UserId == userId)
    .Select(ug => ug.GroupId)

  permissions += GroupRoles
    .Where(gr => directGroupIds.Contains(gr.GroupId))
    .Join(RolePermissions, gr => gr.RoleId, rp => rp.RoleId)
    .Select(rp => (rp.Permission.Name, gr.ScopeType, gr.ScopeValue))

  // 4. Permissions via transitive groups (walk up hierarchy)
  allGroupIds = ExpandGroupHierarchy(directGroupIds) // BFS/DFS

  permissions += GroupRoles
    .Where(gr => allGroupIds.Contains(gr.GroupId))
    .Join(RolePermissions, gr => gr.RoleId, rp => rp.RoleId)
    .Select(rp => (rp.Permission.Name, gr.ScopeType, gr.ScopeValue))

  // 5. Deduplicate and resolve scope conflicts
  return ResolvePermissionConflicts(permissions)
```

## 8. Database Schema (SQL Server)

```
┌─────────────────────┐     ┌─────────────────────┐
│       Users         │     │      UserEmails     │
├─────────────────────┤     ├─────────────────────┤
│ PK Id               │←───│ FK UserId            │
│    Username          │     │    Email             │
│    PasswordHash      │     │    IsPrimary         │
│    IsActive          │     │    IsVerified        │
│    IsLocked          │     │    VerifiedAt        │
│    CreatedAt         │     └─────────────────────┘
│    UpdatedAt         │
└─────────────────────┘     ┌─────────────────────┐
          │                 │    UserClaims        │
          │←────────────────┤    UserLogins        │
          │                 │    UserSessions      │
          │                 │    TotpSecrets       │
          │                 │    BackupCodes       │
          │                 └─────────────────────┘
          │
┌─────────────────────┐     ┌─────────────────────┐
│   UserRoles         │     │      Roles           │
├─────────────────────┤     ├─────────────────────┤
│ FK UserId           │────>│ PK Id               │
│ FK RoleId           │     │    Name              │
│    ScopeType (n)    │     │    Description       │
│    ScopeValue (n)   │     └─────────────────────┘
└─────────────────────┘               │
                                      │
┌─────────────────────┐     ┌─────────────────────┐
│  RolePermissions    │     │    Permissions       │
├─────────────────────┤     ├─────────────────────┤
│ FK RoleId           │────>│ PK Id               │
│ FK PermissionId     │     │    Name              │
│    ScopeType (n)    │     │    Description       │
│    ScopeValue (n)   │     │    ResourceType      │
└─────────────────────┘     └─────────────────────┘

┌─────────────────────┐     ┌─────────────────────┐
│   UserGroups        │     │      Groups          │
├─────────────────────┤     ├─────────────────────┤
│ FK UserId           │     │ PK Id               │
│ FK GroupId          │────>│    Name              │
└─────────────────────┘     │    Description       │
          │                 │ FK ParentGroupId (n) │
          │                 │    (self-referencing) │
          │                 └─────────────────────┘
          │                           │
          │                 ┌─────────────────────┐
          │                 │   GroupRoles         │
          │                 ├─────────────────────┤
          │                 │ FK GroupId           │
          │                 │ FK RoleId            │
          │                 │    ScopeType (n)     │
          │                 │    ScopeValue (n)    │
          │                 └─────────────────────┘
          │
┌─────────────────────┐     ┌─────────────────────┐
│  UserPermissions    │     │     Permissions      │
│  (direct)           │     │     (shared)         │
├─────────────────────┤     └─────────────────────┘
│ FK UserId           │
│ FK PermissionId     │
│    ScopeType (n)    │
│    ScopeValue (n)   │
└─────────────────────┘

┌─────────────────────┐     ┌─────────────────────┐
│    Platforms         │     │     Clients          │
├─────────────────────┤     ├─────────────────────┤
│ PK Id               │←───│ FK PlatformId (n)    │
│    Name              │     │ PK Id               │
│    Description       │     │    ClientId          │
│    PermissionMode    │     │    ClientSecret      │
└─────────────────────┘     │    Name              │
                            │    ClientType        │
                            │    RequirePkce       │
                            │    RequireConsent    │
                            └─────────────────────┘
                                      │
                            ┌─────────────────────┐
                            │ ClientGrantTypes     │
                            │ ClientScopes         │
                            │ ClientRedirectUris   │
                            └─────────────────────┘

┌─────────────────────┐     ┌─────────────────────┐
│ AuthorizationCodes  │     │    RefreshTokens     │
├─────────────────────┤     ├─────────────────────┤
│ PK Id               │     │ PK Id               │
│    Code              │     │    Token             │
│ FK ClientId         │     │ FK ClientId         │
│ FK UserId           │     │ FK UserId           │
│    RedirectUri       │     │    Scopes            │
│    Scopes            │     │    PreviousTokenId  │
│    CodeChallenge     │     │    IsRevoked         │
│    IsUsed            │     │    CreatedAt         │
│    CreatedAt         │     │    ExpiresAt         │
│    ExpiresAt         │     └─────────────────────┘
└─────────────────────┘

┌─────────────────────┐     ┌─────────────────────┐
│    DeviceCodes       │     │    AuditLog          │
├─────────────────────┤     ├─────────────────────┤
│ PK Id               │     │ PK Id               │
│    DeviceCode        │     │    ActorId           │
│    UserCode          │     │    ActorType         │
│ FK ClientId         │     │    Action            │
│ FK UserId           │     │    TargetId          │
│    Scopes            │     │    TargetType        │
│    IsAuthorized      │     │    Details           │
│    IsUsed            │     │    Outcome           │
│    CreatedAt         │     │    IpAddress         │
│    ExpiresAt         │     │    UserAgent         │
└─────────────────────┘     │    Timestamp         │
                            └─────────────────────┘

┌─────────────────────┐
│     Events           │
├─────────────────────┤
│ PK Id               │
│    AggregateId       │
│    AggregateType     │
│    EventType         │
│    Data              │
│    Version           │
│    Timestamp         │
└─────────────────────┘
```

## 9. API Design

### 9.1 URL Convention

| Type | Pattern | Example |
|---|---|---|
| OAuth/OIDC | `/connect/{action}` | `/connect/authorize` |
| SAML | `/saml/{action}` | `/saml/sso` |
| Admin API | `/api/v1/{resource}` | `/api/v1/users` |
| UI Pages | `/{page}` | `/login`, `/consent` |

### 9.2 Error Responses (RFC 7807)

```json
{
  "type": "https://identity.dgdevelopment.local/errors/invalid-request",
  "title": "Invalid Request",
  "status": 400,
  "detail": "The 'redirect_uri' parameter is missing.",
  "instance": "/connect/authorize",
  "traceId": "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01"
}
```

### 9.3 OAuth Error Responses

Per RFC 6749, errors from OAuth endpoints use the standard format:

```json
{
  "error": "invalid_grant",
  "error_description": "The authorization code has expired."
}
```

## 10. Key Rotation Strategy

- Signing keys (RSA 2048-bit or ECDSA P-256) managed via JWKS
- Active key + up to N previous keys for validation during rotation
- Rotation period configurable (default: 30 days)
- Keys never hardcoded; generated at startup and persisted securely
- Certificate-based keys for SAML signing

## 11. Security Considerations

- **Passwords**: hashed with Argon2id
- **Secrets**: client secrets hashed with HMAC-SHA256
- **Tokens**: signed with asymmetric keys (RS256/ES256)
- **Transport**: TLS 1.3 enforced for all endpoints
- **PKCE**: mandatory for public clients (S256 only)
- **Refresh Token Rotation**: each use invalidates the old refresh token
- **DPoP**: proof-of-possession binding to prevent token replay
- **Rate Limiting**: on login, token, and userinfo endpoints
- **CORS**: whitelist of allowed origins per client
- **CSP Headers**: Content-Security-Policy on all UI pages
