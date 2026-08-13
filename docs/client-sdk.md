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

**`TokenResponse`**:
```csharp
string AccessToken
string TokenType       // "Bearer"
int ExpiresIn          // seconds
string? IdToken        // OIDC ID Token
string? RefreshToken   // opaque rotating refresh token
string Scope           // space-separated
DateTime IssuedAt
bool IsExpired()
```

**`UserInfo`**:
```csharp
string Sub             // user ID
string? Name
string? Email
bool EmailVerified
string[]? Permissions  // if IdentityManaged platform
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

### Components

| Class | Role |
|---|---|
| `IdentityAuthStateProvider` | Extends `AuthenticationStateProvider`. Handles PKCE, login redirect, token refresh, logout. |
| `IdentityRefreshHandler` | `DelegatingHandler` that injects Bearer token into HTTP requests. Handles 401 by redirecting to logout. |
| `ITokenStore` / `SessionStorageTokenStore` | Stores tokens in `sessionStorage` via JS interop. |
| `BrowserSessionStorage` | `IJSRuntime` wrapper for browser `sessionStorage`. |

### API — `IdentityAuthStateProvider`

| Method | Description |
|---|---|
| `GetAuthenticationStateAsync()` | Returns current auth state. Auto-refreshes expired tokens. |
| `GetLoginUrl()` | Generates PKCE values and returns the authorize URL. |
| `CompleteLoginAsync(code, codeVerifier)` | Exchanges code for tokens, saves to storage, notifies state change. |
| `LogoutAsync()` | Clears tokens from storage, notifies state change. |
| `GeneratePkce()` | Static method returning `(codeVerifier, codeChallenge)` tuple. |

### Auth Flow

```
1. User clicks Login
2. App calls GeneratePkce() → stores verifier locally
3. App calls GetLoginUrl() → redirects browser to /connect/authorize
4. Identity Server → login page → consent
5. Browser redirects back to /callback?code=...
6. App calls CompleteLoginAsync(code, codeVerifier)
7. Tokens stored in sessionStorage
8. AuthenticationState updated → UI reflects logged-in state
```

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
