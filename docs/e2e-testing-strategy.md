# End-to-end testing strategy — DgDevelopment.Identity

## Status

Reference/planning document only. Nothing here is implemented or scheduled — written 2026-08-29
to capture a design discussion so the reasoning doesn't need to be re-derived later. See
"Recommendation" at the bottom for the one thing worth doing regardless of platform.

## The gap this document is about

`DgDevelopment.Identity.IntegrationTests` (see `CONTEXT.md`) drives the IDP server
(`DgDevelopment.Identity.Server`) directly over `WebApplicationFactory<Program>`, scripting the
OAuth flow with a raw `HttpClient` (form POSTs to the login Razor Page, direct calls to
`/api/v1/*`). That proves the **backend** — auth pipeline, permission checks, data layer — behaves
correctly when called correctly. It does **not** render or interact with any actual UI:

- No test hosts `DgDevelopment.Identity.IdentityPlatform` (the Blazor admin app) — nobody clicks a
  button on a rendered page and checks the result.
- No test drives `DgDevelopment.Identity.Client.Maui` — nobody taps "Log in" and checks the
  system browser opens, completes login, and the app receives the callback.
- No bUnit, Playwright, Selenium, or Appium reference exists anywhere in the repo today.

This document is about closing that gap for the MAUI client specifically (per the conversation
that prompted it), with a shorter section on the admin UI for comparison since the same gap
exists there.

## Why MAUI's login flow resists ordinary testing

The MAUI client uses `Microsoft.Maui.Authentication.WebAuthenticator` (see
`Services/AuthSession.cs`, `Services/Pkce.cs`) for the authorization-code + PKCE flow:

1. The app generates a PKCE `code_verifier`/`code_challenge` pair and opens
   `/connect/authorize?...` in the **system browser** (not an in-app WebView) via
   `WebAuthenticator.Default.AuthenticateAsync(...)`.
2. The user authenticates on the IDP's own web UI, running as a separate process/context outside
   the app entirely.
3. The IDP redirects to the app's registered custom URI scheme (`dgidentityapp://callback`,
   `AppConfig.RedirectUri`). The OS (not the app) intercepts this and reactivates the app, handing
   the callback URL back to the waiting `WebAuthenticator` call.
4. The app exchanges the authorization code for tokens via `IdentityClient`
   (`DgDevelopment.Identity.Client.Core`) and stores them (`SecureStorageTokenStore`).

Step 3 is the crux: it's an OS-level handoff between two separate processes (the app and the
system browser), which is exactly the kind of interaction unit tests, and even the
`WebApplicationFactory`-based integration tests, cannot exercise — there's no in-process seam to
call into. Only real UI automation, against a real (or emulated) OS and a real browser, exercises
it.

## Per-platform assessment

### Windows (`net10.0-windows10.0.19041.0`)

The only MAUI target actually buildable and runnable in the current dev environment (per the
`.csproj` comment: iOS/MacCatalyst need a Mac). Runs unpackaged; the custom URI scheme is
registered via `Platforms/Windows/Package.appxmanifest`.

- **Tooling**: the app renders through WinUI3 (`Microsoft.UI.Xaml`), which exposes a standard
  Windows UI Automation (UIA) tree. **FlaUI** (actively maintained, wraps UIA3) is the more
  current choice; **WinAppDriver** (Microsoft's older Appium-compatible driver) is the more
  established but less actively developed alternative. Either can drive the MAUI window; neither
  natively "sees into" the separately-launched system browser window, which needs its own
  automation session (a second FlaUI/UIA session, or a browser-specific driver like Playwright
  pointed at the same browser instance) coordinated with the app-side one.
- **Feasibility**: highest of the three MAUI targets — no emulator, runs on the same machine/CI
  runner as the build. The main engineering cost is coordinating two automation contexts (app +
  browser) around a single OS-level handoff, and handling whatever account-picker/consent screens
  appear in between.
- **CI cost**: a Windows GitHub Actions runner can run WinUI3 UI automation, but interactive UI
  tests need a real (non-headless) desktop session, which typically means self-hosted runners or
  specific CI configuration (e.g. auto-login, no RDP-disconnected session) — more setup than a
  plain `dotnet test` job, though well short of the cost of the mobile platforms below.

### Android (`net10.0-android`)

- **Tooling**: **Appium** with the UiAutomator2 driver, against an Android emulator (AVD) or a
  real device. `Platforms/Android/WebAuthenticationCallbackActivity.cs` is the intent-filter
  receiver for the custom scheme — Android's Custom Tabs (what `WebAuthenticator` opens on this
  platform) are a well-trodden pattern for OAuth UI testing (AppAuth-style flows), generally more
  automation-friendly than the desktop browser+URI-scheme dance, since Chrome Custom Tabs still
  expose a standard UiAutomator-visible view hierarchy.
- **Feasibility**: needs an emulator — not currently set up in this environment. `AndroidManifest.xml`
  and the callback activity already exist, so the *app* side is ready; what's missing is the
  emulator/device + Appium server infrastructure itself.
- **CI cost**: real cost. GitHub Actions supports Android emulators (e.g. the
  `reactivecircus/android-emulator-runner` action, or a self-hosted runner with KVM), but boot
  time alone adds several minutes per run, on top of Appium server setup and general emulator
  flakiness (timing, ANRs, cold-boot variance).

### iOS / MacCatalyst (`net10.0-ios`, `net10.0-maccatalyst`)

- **Tooling**: Appium with the XCUITest driver, or Xcode's own XCUITest directly.
- **Feasibility**: blocked on the same constraint as just *building* these targets today — needs a
  Mac (Xcode toolchain, code signing). Nothing here is buildable, let alone testable, without one.
- **CI cost**: macOS CI runners (GitHub-hosted or self-hosted) are the most expensive tier
  available, before any test-writing cost is even considered.

## The admin UI (`IdentityPlatform`, Blazor Server + WASM) — for comparison

Same underlying gap (no rendered-UI test exists), but a cheaper problem: no OS-level handoff, no
separate browser process, no emulator. Two tiers, both far more mature/documented than mobile UI
automation:

- **bUnit** — renders Razor components in memory, no real browser. Tests component logic,
  parameter binding, conditional rendering (e.g. `RequirePermission`-gated nav items), event
  handlers. Fast, no flakiness from browser timing. Doesn't exercise real HTTP calls, real
  navigation, or JS interop timing unless those are mocked.
- **Playwright** (`Microsoft.Playwright`, real browser automation, headless-capable) — can drive
  `IdentityPlatform` hosted via `WebApplicationFactory`/Kestrel test server exactly like the
  existing `IntegrationTests` project hosts the IDP, but pointed at with a real (headless)
  browser. This combination (ASP.NET Core test host + Playwright) is well-established for Blazor
  apps specifically and is considerably less flaky than any of the mobile options above, since
  there's no OS-level app/browser handoff to coordinate — everything happens inside one browser
  session against one test server.

If UI-level testing is ever prioritized, this is very likely the better first investment relative
to MAUI: same category of gap, much lower cost and flakiness to close.

## The cheaper alternative: split the untestable seam from the testable logic

Regardless of platform, the highest-value, lowest-cost step is decoupling what actually needs a
real OS/browser from what doesn't:

- **PKCE generation** (`Pkce.cs`), **token exchange and refresh** (`IdentityClient`,
  `AuthSession.EnsureFreshTokenAsync`), and **token storage** (`ITokenStore`,
  `SecureStorageTokenStore`) have no UI dependency at all. These can be integration-tested today
  against a real IDP test host using the same `WebApplicationFactory<Program>` pattern the
  existing `IntegrationTests` project already uses — just driven through `IdentityClient` instead
  of a raw scripted `HttpClient`. This is the change most likely to catch a real regression (a
  PKCE mismatch, a broken refresh path, a token parsing change) and costs nothing close to UI
  automation. Already proposed separately from this document; not repeated here in detail.
- A further, optional step for the app itself: introduce a seam (e.g. an injectable
  `IWebAuthenticator` wrapping `WebAuthenticator.Default`) so a UI test can substitute a fake that
  completes instantly with a canned callback URL, letting UI automation verify the app's own
  screens/state transitions (login button → loading → main page → MFA screen) without ever
  touching a real system browser. This still needs *some* UI automation tooling per platform
  (Section above), but removes the OS handoff as a dependency, which is the single biggest source
  of flakiness in every option discussed above.

## Recommendation

Do the SDK-level integration tests (no UI tooling, no new dependencies, reuses the existing
`IntegrationTests` project's pattern) regardless of what happens with UI-level E2E — that is
where regressions are most likely to hide silently today.

For actual UI-level E2E, if and when it's prioritized:

1. **Windows first** (MAUI) or **Playwright** (admin UI) — whichever platform's regression risk
   matters more at the time — both are achievable in a normal dev/CI environment without new
   infrastructure spend beyond the driver/test-project setup itself.
2. **Android** — worth it once the app has enough real usage to justify emulator + Appium CI
   infrastructure cost; not before.
3. **iOS/MacCatalyst** — blocked on Mac hardware/CI access, same as building those targets at all
   today; revisit if/when that constraint changes.

None of this is scheduled. Revisit when UI regressions actually start happening, or when a
platform's usage/risk profile changes enough to justify the setup cost.
