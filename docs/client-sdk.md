# Client SDK — DgDevelopment.Identity

## Overview

DgDevelopment.Identity provides client SDKs for multiple platforms to simplify integration with the Identity Server.

## Available SDKs

| SDK | Package | Status |
|---|---|---|
| .NET Core | `DgDevelopment.Identity.Client.Core` | ✅ Implemented |
| Blazor | `DgDevelopment.Identity.Client.Blazor` | ✅ Implemented |
| WPF | `DgDevelopment.Identity.Client.Wpf` | ⬜ Planned |
| MAUI | `DgDevelopment.Identity.Client.Maui` | ⬜ Planned |
| React | `@dgdevelopment/identity-client` | ⬜ Planned |

---

## .NET Core SDK (`Client.Core`)

Base library providing OIDC/OAuth communication with the Identity Server. All platform-specific SDKs depend on this.

### Configuration — `OidcOptions`

```csharp
var options = new OidcOptions
{
    Authority = "https://identity.dgdevelopment.local",
    ClientId = "admin-ui",
    ClientSecret = "...",
    RedirectUri = "https://myapp.local/callback",
    PostLogoutRedirectUri = "https://myapp.local/signout-callback",
    Scopes = ["openid", "profile", "email"]
};
```

### API — `IdentityClient`

| Method | Description |
|---|---|
| `GetAuthorizeUrl(state?, codeChallenge?)` | Returns the full `/connect/authorize` URL with PKCE parameters |
| `GetLogoutUrl(idTokenHint?)` | Returns the `/connect/endsession` URL |
| `ExchangeCodeAsync(code, codeVerifier)` | Exchanges authorization code for tokens |
| `RefreshTokenAsync(refreshToken)` | Rotates refresh token, returns new tokens |
| `GetUserInfoAsync(accessToken)` | Fetches user claims from `/connect/userinfo` |

### Models

Client DTOs are JSON-deserialized with `JsonPropertyName`, aligned to the IDP **snake_case** responses.

**`TokenResponse`**:
```csharp
string AccessToken       // access_token
string TokenType         // token_type ("Bearer")
int ExpiresIn            // expires_in (seconds)
string? IdToken          // id_token
string? RefreshToken     // refresh_token (opaque rotating)
string Scope             // scope (space-separated)
DateTime IssuedAt        // issued_at
bool IsExpired()
```

**`UserInfo`**:
```csharp
string Sub               // sub (user ID)
string? Name             // name
string? Email            // email
bool EmailVerified       // email_verified
string[]? Permissions    // permissions (if IdentityManaged platform)
```

---

## Blazor SDK (`Client.Blazor`)

Razor Class Library for Blazor (WASM and Hybrid) providing authentication state, token management, and DI extensions.

### Registration

```csharp
// Program.cs
builder.Services.AddIdentityAuthentication(new OidcOptions
{
    Authority = "https://identity.dgdevelopment.local",
    ClientId = "admin-ui",
    ClientSecret = "...",
    RedirectUri = builder.HostEnvironment.BaseAddress + "callback",
    PostLogoutRedirectUri = builder.HostEnvironment.BaseAddress + "signout-callback"
});
```

`AddIdentityAuthentication` registers: `OidcOptions` (singleton), `ISessionStorageService`/`BrowserSessionStorage`, `ITokenStore`/`SessionStorageTokenStore`, `ISessionMarkerService`/`SessionMarkerService`, `IdentityAuthStateProvider` (as `AuthenticationStateProvider`), `IdentityRefreshHandler` and cascading authentication state.

### Components

| Class | Role |
|---|---|
| `IdentityAuthStateProvider` | Extends `AuthenticationStateProvider`. Handles PKCE, login redirect, token refresh, userinfo fetch (cached), logout, session marker. |
| `ServerIdentityAuthStateProvider` | (AdminUi server host) Derived provider that restores the principal from the `identity_marker` cookie during SSR prerender — no network calls. |
| `SessionStorageTokenStore` | `ITokenStore` using browser `sessionStorage` (key `identity_tokens`). |
| `SessionMarkerService` | `ISessionMarkerService` that mirrors `sub`/`name`/`email` into the `identity_marker` cookie via JS interop. |
| `IdentityRefreshHandler` | `DelegatingHandler` that injects Bearer token into HTTP requests. Handles 401 by redirecting to logout. |
| `BrowserSessionStorage` | `IJSRuntime` wrapper for browser `sessionStorage`. |

### API — `IdentityAuthStateProvider`

| Method | Description |
|---|---|
| `GetAuthenticationStateAsync()` | Returns current auth state. Auto-refreshes expired tokens, fetches `/connect/userinfo` once (cached), sets the session marker. |
| `GetLoginUrl(state?, codeChallenge?)` | Returns the authorize URL (appends a `nonce`). |
| `CompleteLoginAsync(code, codeVerifier)` | Exchanges code for tokens, saves to `sessionStorage`, builds the principal, sets the marker, notifies. |
| `LogoutAsync()` | Clears tokens + marker, notifies state change. |
| `GeneratePkce()` | Static method returning `(codeVerifier, codeChallenge)` tuple (S256). |

### Auth Flow

```
1. User clicks Login
2. App calls GeneratePkce() → stores verifier locally
3. App calls GetLoginUrl() → redirects browser to /connect/authorize
4. Identity Server → login page → consent (currently a stub — skipped)
5. Browser redirects back to /callback?code=...
6. App calls CompleteLoginAsync(code, codeVerifier)
7. Tokens stored in sessionStorage (identity_tokens) + marker cookie set
8. AuthenticationState updated → UI reflects logged-in state
```

> During SSR prerender, `ServerIdentityAuthStateProvider` restores the principal from the `identity_marker` cookie, avoiding a `userinfo` round-trip before the WASM runtime loads.

### Token Refresh

- `GetAuthenticationStateAsync()` checks if access token is expired
- If expired and refresh token exists, calls `RefreshTokenAsync()`
- Rotation: old refresh token is revoked, new one is saved
- If refresh fails, user is logged out

### HTTP Handler

```csharp
// Automatically adds Bearer token to all requests
builder.Services.AddHttpClient("identity")
    .AddHttpMessageHandler<IdentityRefreshHandler>();
```

---

## WPF SDK (`Client.Wpf`) — Planned

Will support:
- System browser or WebView2 for login
- DPAPI-protected token storage
- `IdentityHttpMessageHandler` for HttpClient

## MAUI SDK (`Client.Maui`) — Planned

Will support:
- Web Authenticator Browser for login
- `SecureStorage` for tokens
- DI integration

## React SDK (`@dgdevelopment/identity-client`) — Planned

Will provide:
- React Context for auth state
- Hooks: `useAuth()`, `useUser()`, `usePermissions()`
- Components: `<LoginButton>`, `<ProtectedRoute>`
