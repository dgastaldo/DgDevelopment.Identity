# Client SDK — DgDevelopment.Identity

## Overview

DgDevelopment.Identity provides client SDKs for multiple platforms to simplify integration with the Identity Server.

## Available SDKs

| SDK | Package | Status |
|---|---|---|
| .NET Core | `DgDevelopment.Identity.Client.Core` | ✅ Implemented |
| Blazor | `DgDevelopment.Identity.Client.Blazor` | ✅ Implemented |
| MAUI | `DgDevelopment.Identity.Client.Maui` | 🟡 First cut (Android/Windows; iOS/MacCatalyst need a Mac toolchain) |
| WPF | `DgDevelopment.Identity.Client.Wpf` | ⬜ Planned |
| React | `@dgdevelopment/identity-client` | ⬜ Planned |

---

## .NET Core SDK (`Client.Core`)

Base library providing OIDC/OAuth communication with the Identity Server. All platform-specific SDKs depend on this.

### Configuration — `OidcOptions`

```csharp
var options = new OidcOptions
{
    Authority = "https://identity.dgdevelopment.local",
    ClientId = "<client-guid>",
    ClientSecret = "...",
    RedirectUri = "https://myapp.local/callback",
    PostLogoutRedirectUri = "https://myapp.local/signout-callback",
    Scopes = ["openid", "profile", "email"]
};
```

### API — `IdentityClient`

Core OIDC flow:

| Method | Description |
|---|---|
| `GetAuthorizeUrl(state?, codeChallenge?, tenant?)` | Returns the full `/connect/authorize` URL with PKCE parameters, optionally targeting a specific tenant slug |
| `GetLogoutUrl(idTokenHint?)` | Returns the `/connect/endsession` URL |
| `ExchangeCodeAsync(code, codeVerifier, redirectUri?)` | Exchanges authorization code for tokens |
| `RefreshTokenAsync(refreshToken)` | Rotates refresh token, returns new tokens |
| `GetUserInfoAsync(accessToken)` | Fetches user claims from `/connect/userinfo` |
| `GetMyTenantsAsync()` | Lists the caller's tenant memberships and whether they're a global administrator (`/me/tenants`) |

Self-service (any signed-in user, `/api/v1/me/*` and `/api/v1/account/mfa/*`):

| Method | Description |
|---|---|
| `GetMeAsync()` | The caller's own profile (`/me`) |
| `ChangePasswordAsync(currentPassword, newPassword)` | Self password change - subject to the full password policy (complexity, common-password blocklist, reuse history) and revokes every other session/refresh token on success |
| `AddEmailAsync(email)` / `RemoveEmailAsync(email)` / `SetPrimaryEmailAsync(email)` | Self email management, each new address needs its own verification link |
| `GetTotpStatusAsync()` / `EnrollTotpAsync()` / `EnableTotpAsync(code)` / `DisableTotpAsync()` / `RegenerateBackupCodesAsync()` | Self-service TOTP enrollment/management - the same flow the login-time MFA step offers, reachable without a fresh login |
| `GetPushDevicesAsync()` / `RegisterPushDeviceAsync(platform, pushToken, deviceName?, totpCode?)` / `RemovePushDeviceAsync(id)` | Push-MFA device registry |

Admin API (`/api/v1/{users,roles,permissions,groups,platforms,clients,tenants,audit,dashboard}`, `[RequirePermission]`-gated server-side): `IdentityClient` exposes the full CRUD surface 1:1 with the admin API - `GetUsersAsync`/`CreateUserAsync`/`LockUserAsync`/`ResetPasswordAsync`/`AssignUserRoleAsync`/etc., and the equivalents for roles, permissions, groups, platforms, clients, and tenants. See the method list directly in `IdentityClient.cs` rather than duplicating ~50 method signatures here.

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
    ClientId = "<client-guid>",
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
| `GetAuthenticationStateAsync()` | Returns current auth state. Auto-refreshes expired tokens, fetches `/connect/userinfo` once (cached), sets the session marker, opens the real-time session-event listener (see below). |
| `GetLoginUrl(state?, codeChallenge?, tenant?)` | Returns the authorize URL (appends a `nonce`). |
| `CompleteLoginAsync(code, codeVerifier, redirectUri?)` | Exchanges code for tokens, saves to `sessionStorage`, builds the principal, sets the marker, opens the session-event listener, notifies. |
| `LogoutAsync()` | Stops the session-event listener, clears tokens + marker, notifies state change. |
| `GeneratePkce()` | Static method returning `(codeVerifier, codeChallenge)` tuple (S256). |
| `ForceLoggedOut` (event) | Raised when the server pushes a real-time force-logout event for this user (see below) - tokens are already cleared by the time it fires. `IdentityPlatform.Client`'s `MainLayout.razor` subscribes to redirect to the login page. |

### Auth Flow

```
1. User clicks Login
2. App calls GeneratePkce() → stores verifier locally
3. App calls GetLoginUrl() → redirects browser to /connect/authorize
4. Identity Server → login page → consent (approve/deny, admin-approved scopes skip the prompt)
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

## Real-time session events (`SessionEventClient`, in `Client.Core`)

Shared by every platform - the protocol (connect, subscribe to a per-user group, listen for one named message) has nothing platform-specific about it. Connects to the IDP's `/hubs/session` SignalR hub with a Bearer access token (via `AccessTokenProvider`, re-invoked on every reconnect so an expired token gets refreshed rather than captured once) and raises `ForceLogoutReceived` when the server pushes a force-logout event - a password changed on another device, or a session was otherwise revoked.

| Method | Description |
|---|---|
| `StartAsync(authority, userId, accessTokenProvider, ct?)` | Connects and subscribes to `userId`'s channel. Stops any existing connection first - safe to call again after a fresh login. |
| `StopAsync()` | Closes the connection. |
| `IsConnected` | Whether the underlying `HubConnection` is currently connected. |
| `ForceLogoutReceived` (event) | Raised when the server pushes the `"ForceLogout"` message for the subscribed user. |

Both `AuthSession` (MAUI) and `IdentityAuthStateProvider` (`Client.Blazor`) wrap this: they open the connection once a user is signed in, clear their own stored tokens and raise their own `ForceLoggedOut` event on the push, and stop the connection on logout - see the dedicated CONTEXT.md bullet for the full session-revocation feature this backs. `ServerIdentityAuthStateProvider` (`IdentityPlatform`'s SSR prerender path) never actually opens a connection, since it has no real tokens to authenticate a hub connection with.

## MAUI SDK (`Client.Maui`)

`DgDevelopment.Identity.Client.Maui` - targets Android and Windows for now (iOS/MacCatalyst need a Mac toolchain to build past the C# entry point).

- **`AuthSession`** - drives login via `Microsoft.Maui.Authentication.WebAuthenticator` (system browser, PKCE, no client secret - the seeded MAUI client is `ClientType.Public`) and holds the current session's tokens. `LoginAsync()`, `LogoutAsync()`, `EnsureFreshTokenAsync()` (call before any authenticated API call), `IsAuthenticatedAsync()`, `EnsureSessionListenerStartedAsync()` (opens the real-time force-logout channel, idempotent - safe to call from every page's `OnAppearing`), and a `ForceLoggedOut` event.
- **`ITokenStore`/`SecureStorageTokenStore`** - tokens persisted via MAUI `SecureStorage`.
- **`IdentityAuthHandler`** - a `DelegatingHandler` that attaches the current access token to every request and clears it on a 401; registered via `AddHttpMessageHandler<IdentityAuthHandler>()` on the `IdentityClient` `HttpClient`.
- **`Pkce`** - PKCE code-verifier/challenge generation (S256).
- **App wiring**: `App.xaml.cs` subscribes to `AuthSession.ForceLoggedOut` and navigates back to `LoginPage` on the UI thread (`MainThread.BeginInvokeOnMainThread` - the SignalR callback isn't on it). Pages: `LoginPage`, `MainPage`, `MfaPage` (self-service TOTP enroll/enable/disable, push-device register/remove - the same endpoints `IdentityPlatform`'s own `/mfa` page uses). Push-device registration currently sends a locally-generated placeholder token - no real FCM/APNs push SDK is wired up yet.

## WPF SDK (`Client.Wpf`) — Planned

Will support:
- System browser or WebView2 for login
- DPAPI-protected token storage
- `IdentityHttpMessageHandler` for HttpClient

## React SDK (`@dgdevelopment/identity-client`) — Planned

Will provide:
- React Context for auth state
- Hooks: `useAuth()`, `useUser()`, `usePermissions()`
- Components: `<LoginButton>`, `<ProtectedRoute>`
