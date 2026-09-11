# Deployment pipeline — DgDevelopment.Identity

## Status

Reference/planning document only. Nothing here is implemented yet, except the Redis cleanup (see
"Findings" below), which is done — written 2026-08-29 to capture a design discussion so the
reasoning doesn't need to be re-derived later, and so we can keep iterating on it here before any
`AppHost.cs`/workflow code is written. See "Open items" at the bottom for what's still pending.
Everything else is the current working design, not yet built.

## Goal

Turn the branch-per-stage convention into an actual automated publishing pipeline: pushing a
tag/release on a branch builds and ships that branch's code to its own environment, with GitHub
Actions as the CI/CD engine. Deployment itself goes through the Aspire CLI (`aspire publish`/
`aspire deploy`) rather than hand-rolled Dockerfiles/Helm/Bicep — see "Aspire-native deployment
model" below for why.

## Why `develop` needs its own always-on environment

This isn't just "deploy `develop` continuously because it's the newest code" — it specifically
unblocks real client-side E2E testing. `docs/e2e-testing-strategy.md` already identifies the gap:
no test exercises `IdentityPlatform` (the admin Blazor app) or `DgDevelopment.Identity.Client.Maui`
through a real, rendered UI (Playwright for the admin app; FlaUI/WinAppDriver for MAUI on Windows).
Both of those approaches need something real to point at — a running server, with a real database,
reachable over HTTP, that reflects the latest code. An always-on `develop` deployment is exactly
that target. Nothing about UI-level E2E automation is scheduled yet (see that doc's own
"Recommendation" section), but the backend needs to exist and stay fresh before that work can even
start, which is the actual reason `develop` gets its own continuous deployment rather than staying
CI-only/ephemeral.

## Branch chain and environments

```
develop → personal → docs → integration → main
```

`personal` needs to be re-parented onto `develop` before this works — it was branched off `main` a
few minutes before this doc was written, but `main` is pre-M1, months behind `develop`, and was
only ever meant as a quick starting point. Nothing meaningful has been committed on top of it since,
so re-pointing it at current `develop` (and force-pushing, since `origin/personal` already exists
with that stale, unrelated history) is a clean fix, not a rebase of real work. Once re-parented,
`personal` is a normal link in the chain and receives `develop`'s content — this pipeline design
included — through an ordinary forward merge.

| Branch | Environment | Host | Deploy trigger | Approval |
|---|---|---|---|---|
| `develop` | develop/e2e | home PC (self-hosted runner), Docker Compose | every push (incl. PR merge) | none — auto |
| `personal` | personal | same home PC, separate Compose stack | `personal/v*` tag | none — auto |
| `docs` | docs | Azure App Service | `docs/v*` tag | manual (GitHub Environment) |
| `integration` | integration | AKS — first publicly-reachable tier ("preview prod") | `integration/v*` tag | manual |
| `main` | production | AKS — separate cluster/resource group from integration | `v*` (bare) tag | manual |

Every branch runs **identical application code**. Environments differ only in deploy-time
configuration — DB connection string, secrets, resource group/cluster names — never in code. That
also means promotion PRs between chain branches should almost always be trivial fast-forwards.

**`docs` isn't just a stage name** — despite being early in the chain, it's meant to be genuinely
public: it hosts the IDP's API documentation (the existing OpenAPI/Scalar/Swagger surface already
in `DgDevelopment.Identity.Server`) for third-party integrators, distinct from `integration` (the
first publicly-reachable *application* tier). Worth carrying this same clarification into
`CONTEXT.md` wherever the forward-merge chain is described, so a future reader doesn't read `docs`
as just a pre-release naming artifact.

## Promotion cascade

Two distinct automations, not one:

1. **`develop`**: every push triggers its own deploy workflow directly — no tag, no approval gate.
   Nothing upstream of `develop` auto-opens anything into it; regular feature PRs merge into it
   exactly as today.
2. **`personal → docs → integration → main`**: promoting `develop`'s current state into `personal`
   is a manual, on-demand action — a PR you open yourself when you actually want to publish. From
   there, promotion cascades automatically: merging a promotion PR into `personal`, `docs`, or
   `integration` auto-opens the *next* hop's PR (`personal→docs`, `docs→integration`,
   `integration→main`) via a `pull_request: closed` (`merged == true`) workflow. Each hop still
   needs a human merge click — that's the promotion approval gate. **That same workflow also
   auto-creates and pushes that branch's deploy tag** (see "Tag naming convention" below) right
   after the merge, which is what actually fires the deploy workflow — so there's no separate manual
   tagging step. For `docs`/`integration`/`production` there's still a second, later checkpoint: the
   deploy workflow itself pauses at its GitHub Environment's approval gate before `aspire deploy`
   actually runs, distinct from (and after) the promotion merge click.

## Tag naming convention (app deploy tags)

This section is about the *app's* deploy tags only — `personal`/`docs`/`integration`/production
snapshots of `Server`/`IdentityPlatform`. The `clients/*` projects (`Client.Core`, `Client.Blazor`,
`Client.Maui`, `Client.Wpf`) use a deliberately separate, real-semver scheme — see "Client
package/app versioning" below.

**Decided: deploy tags are created automatically, not by hand.** You don't know the tag by name in
advance and don't need to — a promotion PR merging into a branch is what triggers its tag, via
`promote.yml` (and, for `develop`, its own deploy workflow doesn't need a tag at all — it deploys on
every push). Since promotion merges are fast-forwards carrying no independent code changes (point
confirmed above — the coverage/CodeQL gate only needs to run once, on the PR into `develop`), there's
no meaningful "major/minor/patch" decision to automate around for *these* tags; each one is just a
deployment marker, not a compatibility promise anyone depends on. Proposed scheme:
`<prefix><date>.<run-number>`, generated in
`promote.yml` from `date +%Y.%m.%d` and `${{ github.run_number }}` (auto-incrementing, unique per
workflow, needs no external counter):

| Environment | Tag pattern | Example |
|---|---|---|
| personal | `personal/v*` | `personal/v2026.08.30.1` |
| docs | `docs/v*` | `docs/v2026.08.30.1` |
| integration | `integration/v*` | `integration/v2026.08.30.1` |
| production | `v*` (bare) | `v2026.08.30.1` |

`promote.yml`, after a promotion PR merges into branch X: computes this tag name, `git tag` +
`git push` it against X's new HEAD (this push is what triggers `deploy-X.yml`'s tag-based trigger),
then — if X has a next branch in the chain — opens the `X→next` promotion PR. One workflow, two
side effects.

## Client package/app versioning (`Client.Core`, `Client.Blazor`, `Client.Maui`, `Client.Wpf`)

**Decided**: a separate, real-semver scheme from the app deploy tags above, driven automatically by
Conventional Commits — and it applies to **all four** `clients/*` projects, not just the two NuGet
libraries. `Client.Core`/`Client.Blazor` ship to NuGet, where consumers depend on the version number
meaning something (breaking change → major, new capability → minor, fix → patch); `Client.Maui`/
`Client.Wpf` are apps (not NuGet packages — `OutputType=Exe`) but still need their own independent
version numbers (MSIX/store package version, `ApplicationDisplayVersion` in the `.csproj`, etc.) on
their own release cadence, for whatever their eventual distribution channel turns out to be. Four
independent components, same versioning mechanism.

**Proposed tool: [release-please](https://github.com/googleapis/release-please)** (Google), not
semantic-release — the deciding factor is that this is a **monorepo with independent components**,
each needing its own version/changelog/release cadence based only on commits touching its own path.
release-please is built for exactly that (per-path "components," each with its own manifest entry
and tag prefix), and — unlike a fully silent auto-tag-on-push tool — maintains a standing **Release
PR** per component that accumulates the changelog and computed version bump as commits land; merging
that PR is what actually creates the tag and fires whatever publish step follows. That gives you a
review/approval moment for free, in the same spirit as this design's other promotion gates, rather
than every commit silently shipping a new release. For `Client.Core`/`Client.Blazor`, release-please
has a native .NET/NuGet release type that bumps the version directly in the `.csproj`; for
`Client.Maui`/`Client.Wpf` the "release type" is more generic (bump `ApplicationDisplayVersion`/
equivalent) since there's no NuGet publish step to hand off to yet — see the open question below.

Consequence worth flagging: this needs **Conventional Commit discipline** going forward on commits
touching any of the four `clients/*` project directories — release-please can't compute a bump from
a commit message that doesn't follow the convention. Not a concern for commits touching only `src/`
or `tests/`.

**How the bump is decided** — from the prefix of each commit message touching that component's
path, since the last release for it; the highest-priority type found wins (one `feat!:` among ten
`fix:`s still forces a major bump):

| Commit prefix | Bump | Example |
|---|---|---|
| `fix: ...` | patch | `fix: correct token expiry check` → `1.2.3` → `1.2.4` |
| `feat: ...` | minor | `feat: add tenant switcher method` → `1.2.3` → `1.3.0` |
| `feat!: ...` / `fix!: ...` / `BREAKING CHANGE:` footer | major | `feat!: rename GetTenants to ListTenants` → `1.2.3` → `2.0.0` |
| `chore:`, `docs:`, `refactor:`, `test:`, `style:`, `ci:`, `build:` | none | no version change |

**How a release actually happens**: merging the standing Release PR is the only signal needed — the
bot has already computed everything from the accumulated commits. If the proposed version is wrong
for some reason (e.g. you want to jump to `2.0.0` for reasons the commits don't reflect), push a
commit to the Release PR editing the version directly before merging it — an escape hatch, not the
normal path.

Tag prefixes (release-please's default per-component format), independent version numbers since one
component changing shouldn't bump another's: `client-core-v1.2.0`, `client-blazor-v1.2.0`,
`client-maui-v1.0.0`, `client-wpf-v1.0.0` (the latter presumably starting once `Client.Wpf` is more
than a scaffold).

**Decided: `develop` feeds release-please.** Keeps package/app release cadence independent of the
app's own promotion cascade — a library fix doesn't need to wait for someone to decide to promote
the *application* to `docs`/`integration` before it can ship. The human gate is the Release PR merge
itself, same quality control either way; tying it to a later branch would just add an unrelated
dependency on app-promotion timing for no real safety benefit.

### `Client.Maui` distribution — decided

**GitHub Releases**, not the Microsoft Store/Google Play — zero cost, no developer account, no
submission/review process. There's no store discoverability/auto-update — acceptable for where this
project is now, revisit if/when that changes.

`Client.Wpf` is still a scaffold with no real code, so this only applies to `Client.Maui` today —
its own release-please component won't produce meaningful releases until that changes, and its
distribution question stays open until there's something to distribute.

**Both Windows formats get published side by side**, so both can actually be tried: the plain
unpackaged exe (zipped, no install needed, no cert required) and a real **MSIX** installer package.

MSIX changes the signing story — unlike the loose exe (unsigned + a SmartScreen warning is enough
to run it), Windows won't install an MSIX at all without *some* valid signing certificate; there's
no unsigned option. A **self-signed certificate** covers this for now (free, five minutes to
generate) rather than a paid CA certificate — the real cost of that choice is that whoever installs
the MSIX has to explicitly trust that certificate first (import its public half into "Trusted
People" or "Trusted Root Certification Authorities"), which is a one-time, slightly annoying step
per machine, not something a random stranger would click through — fine for personal/testing use,
not for anonymous public distribution. Revisit with a real CA certificate if that ever matters.

One-time manual setup (added to "Manual, out-of-band setup" below): generate the self-signed
certificate (`New-SelfSignedCertificate`), export it as a password-protected `.pfx`, and store the
base64-encoded `.pfx` plus its password as two new repository secrets
(`WINDOWS_SIGNING_CERT_BASE64`, `WINDOWS_SIGNING_CERT_PASSWORD`) — a repo secret rather than a
GitHub Environment one, since `publish-client-maui.yml` isn't gated behind an Environment.

New workflow, **`publish-client-maui.yml`**, triggered on `push: tags: ['client-maui-v*']`:

1. Check out at that tag.
2. Build `net10.0-windows10.0.19041.0` (`windows-latest` GitHub-hosted runner — this doesn't need
   the home-PC self-hosted runner, it's a stateless build) twice:
   - Unpackaged (`WindowsPackageType=None`, today's default) → zip the output.
   - Packaged (`WindowsPackageType=MSIX`), signed with the certificate decoded from the two secrets
     above → produces the `.msix`. Exact MSBuild properties (`PackageCertificateKeyFile`/
     `PackageCertificatePassword` or equivalent) to be confirmed against current MAUI/WinUI
     packaging docs at implementation time, not assumed here.
3. Build `net10.0-android` (needs `dotnet workload install maui-android`, works on GitHub-hosted
   runners) — produces an `.apk` directly. iOS/MacCatalyst stay out of scope, same constraint as
   everywhere else in this repo (needs a Mac toolchain).
4. Create a GitHub Release for that tag (`gh release create` or `softprops/action-gh-release`),
   attaching the Windows zip, the Windows `.msix`, and the Android `.apk`, with release notes pulled
   from release-please's own generated changelog for that release. Release notes should mention the
   self-signed-cert trust step for anyone trying the MSIX.

## Aspire-native deployment model

This repo ships a bundled Microsoft skill, `.agents/skills/aspire-deployment`, which explicitly
owns Aspire deployment routing and says not to hand-roll Dockerfiles/Helm/Bicep when an AppHost
already exists (it does: `src/DgDevelopment.Identity.AppHost`). The design here follows that: the
AppHost model plus `aspire publish`/`aspire deploy` handle building, pushing, and provisioning;
GitHub Actions just drives the Aspire CLI with the right credentials/parameters per branch.

Because the chain forward-merges, `AppHost.cs` ends up byte-identical (modulo not-yet-merged
commits) across every branch — maintaining divergent AppHost files per target would fight that
model. Instead, one Aspire deployment parameter (`deploy-target`) gets set per branch's workflow,
and `AppHost.cs` branches on its value to add the one compute-environment resource relevant to that
run:

```csharp
var deployTarget = builder.AddParameter("deploy-target", "local");
// "personal" | "develop" | "docs" | "integration" | "main" | "local"

switch (builder.Configuration["Parameters:deploy-target"])
{
    case "personal":
    case "develop":
        builder.AddDockerComposeEnvironment("docker-compose");
        server.WithExternalHttpEndpoints();
        identityPlatform.WithExternalHttpEndpoints();
        break;
    case "docs":
        builder.AddAzureAppServiceEnvironment("appservice");
        server.WithExternalHttpEndpoints();
        identityPlatform.WithExternalHttpEndpoints();
        break;
    case "integration":
    case "main":
        builder.AddAzureKubernetesEnvironment("aks");
        server.WithExternalHttpEndpoints();
        identityPlatform.WithExternalHttpEndpoints();
        break;
    // default/"local": no environment resource added — today's dev-orchestration behavior, untouched
}
```

Illustrative only — **exact builder method names/overloads get re-verified with
`aspire docs api search "AddAzureAppServiceEnvironment" --language csharp` etc. at implementation
time**, per the skill's explicit rule against inventing APIs. What's already confirmed via the
skill's reference docs and `aspire docs get`:

- `AddDockerComposeEnvironment("name")` — personal/develop.
- `AddAzureAppServiceEnvironment("name")` — docs. App Service is a public-website-only model
  (no arbitrary sidecar containers); moot here since Redis is slated for removal and the DB is
  already external.
- `AddAzureKubernetesEnvironment("name")` — integration/main. This call **provisions** AKS + ACR +
  identity itself, it doesn't just deploy into a pre-existing cluster — no manual "create the AKS
  cluster" step needed. integration and main get separate resource groups/subscription scoping
  (`Azure__ResourceGroup`/`Azure__SubscriptionId`, set per-branch in CI) — real isolation between
  the first public tier and true production, at the cost of two clusters.
- Registry: **GHCR**. AKS auto-creates an ACR by default unless a registry is configured
  explicitly — the override API needs confirming (`aspire docs api search` for container-registry
  customization on `AddAzureKubernetesEnvironment`), plus a GHCR image-pull secret on the cluster
  (PAT or `GITHUB_TOKEN`-based) so pods can pull private images.

## GitHub Actions layout (once implemented)

One reusable workflow with the actual Aspire CLI steps, thin per-branch trigger workflows on top so
each environment's trigger/approval stays declarative:

- **`.github/workflows/_deploy.yml`** (`workflow_call`) — checkout, .NET 10 SDK, Aspire CLI
  install, `dotnet restore`/`build`, GHCR login, Azure login (OIDC, `docs`/`integration`/
  `production` only — `develop`/`personal` need no Azure auth at all, their secrets come straight
  from their GitHub Environment), `aspire deploy --non-interactive` with `Parameters__deploy_target`
  and target-specific `Azure__*`/`Parameters__*` env vars from the caller. Input: which ref/tag to
  check out and deploy — defaults to `github.ref` (the tag that triggered the run), but overridable,
  which is what rollback (below) uses.
- **`deploy-develop.yml`** — `push: branches: [develop]`; self-hosted `home-pc` (Ubuntu) runner;
  `environment: develop` (no required reviewer — scopes secrets only, doesn't gate).
- **`deploy-personal.yml`** — `push: tags: ['personal/v*']` + `workflow_dispatch` (manual rollback,
  see below); same self-hosted runner; `environment: personal` (same no-gate scoping).
- **`deploy-docs.yml`** — `push: tags: ['docs/v*']` + `workflow_dispatch`; `environment: docs`;
  `ubuntu-latest`.
- **`deploy-integration.yml`** — `push: tags: ['integration/v*']` + `workflow_dispatch`;
  `environment: integration`.
- **`deploy-production.yml`** — `push: tags: ['v*']` + `workflow_dispatch`; `environment: production`.
- **`promote.yml`** — the merged-PR cascade (`personal→docs→integration→main`) described above,
  including auto-creating and pushing each branch's deploy tag.

Reference shape for the Azure-targeting jobs already lives in the repo:
[.agents/skills/aspire-deployment/references/github-actions-azure-csharp.yml](../.agents/skills/aspire-deployment/references/github-actions-azure-csharp.yml)
— adapt rather than write from scratch.

## Rollback

**Decided: republish the previous tag.** Nothing fancier — no automatic health-check-triggered
rollback, no blue-green switch. Tags are never deleted, so every previously-deployed state stays
addressable by its tag name indefinitely.

To roll back an environment: trigger that environment's deploy workflow manually
(`workflow_dispatch`, from the Actions tab or `gh workflow run deploy-<env>.yml --ref <old-tag>`)
against the last-known-good tag instead of waiting for a new one. `_deploy.yml` checks out whatever
ref it's given rather than assuming `github.ref`, so this reuses the exact same deploy path as a
normal release — no separate rollback machinery to build or keep in sync. `docs`/`integration`/
`production` still go through their GitHub Environment approval gate on the way, same as any other
deploy.

## Manual, out-of-band setup

None of this can be done by editing the repo:

1. **Self-hosted runner on the home PC** (Ubuntu, not Windows — corrected from an earlier
   assumption) — register from repo Settings → Actions → Runners, choosing Linux/x64, labeled e.g.
   `home-pc`. Needs Docker + Compose installed, and the runner's user in the `docker` group
   (`sudo usermod -aG docker $USER`, then re-login) so it can run `docker compose` without `sudo`.
   Install as a systemd service via the runner package's `sudo ./svc.sh install && sudo ./svc.sh start`
   rather than leaving it running in a foreground terminal.
2. **GitHub Environments** — create all five: `docs`, `integration`, `production` each with a
   required reviewer (the actual approval gate); `personal` and `develop` too, but with **no**
   protection rules — an Environment without a required reviewer still deploys automatically, it
   just gives those two branches a secret-scoping boundary instead of dumping everything into
   repository-wide secrets. This replaces the Key Vault idea entirely (see "Azure infrastructure"
   below) — no Azure dependency needed just to hold a connection string.
3. **Azure** — an app registration with federated credentials (OIDC, `azure/login`) per Azure
   Environment's secrets (`AZURE_CLIENT_ID`/`AZURE_TENANT_ID`/`AZURE_SUBSCRIPTION_ID`), plus
   resource-group name/location as Environment variables — one set for `docs`, two for
   `integration`/`production` (separate resource groups). **`develop`/`personal` need no Azure login
   at all** now that Key Vault is out of the picture — whatever ANH/ACS connection strings they end
   up using are just plain GitHub Environment secrets, no runtime Azure authentication required to
   read them.
4. **GHCR** — confirm package visibility for `ghcr.io/dgastaldo/...` and generate whatever pull
   credential the AKS clusters need (PAT with `read:packages`, stored as an Environment secret,
   turned into a k8s imagePullSecret during deploy).
5. **DB connection strings** — provisioning the actual SQL Server/Azure SQL instance itself stays
   out of scope for this pass (see "Findings" below) — you provision it. The connection string is a
   GitHub Environment secret (`Parameters__IdentityDb`) in all five environments now, `develop`/
   `personal` included (per point 2 above).
6. **Windows MSIX signing certificate** — generate a self-signed code-signing certificate
   (`New-SelfSignedCertificate`), export as a password-protected `.pfx`, store the base64-encoded
   `.pfx` and its password as repository secrets `WINDOWS_SIGNING_CERT_BASE64`/
   `WINDOWS_SIGNING_CERT_PASSWORD` — see "`Client.Maui` distribution" above. Repo-level, not an
   Environment secret, since `publish-client-maui.yml` runs unconditionally on its tag, ungated.

## Azure infrastructure for `develop`/`personal`

`develop` and `personal` still run entirely on a home PC (Ubuntu, Docker Compose, self-hosted
runner) — that hasn't changed, and neither needs an Azure *subscription* just to deploy the app
itself. Two pieces are genuinely cloud-only services the code already calls or will call — nothing
to do with hosting, and both entirely optional to provision right now:

- **Azure Notification Hub (ANH)** — already wired in code (`AzureNotificationHubNotifier`,
  the only `IPushNotifier` implementation) for push-MFA challenge delivery. It no-ops silently if
  unconfigured, so nothing breaks without it, but push won't actually work.
- **Azure Communication Services (ACS)**, for email — not wired in code yet (`SmtpNotificationService`
  is the only `INotificationService` today). Provisioning this now is getting ahead of the code;
  an `IAcsEmailNotificationService`-style implementation is a small follow-up task, not just infra.

Neither is a blocker for the first `develop`/`personal` deploys — the app runs and pushes/emails
just no-op or fall back to logging until these are provisioned. **No Azure Key Vault** either
(dropped from the design — see below): secrets for `develop`/`personal` live directly in their
GitHub Environment secrets, same mechanism as `docs`/`integration`/`production`, no Azure
authentication needed to read them.

Checked the rest of the codebase for other cloud-only *code* dependencies (Application Insights,
Service Bus, Event Grid, Blob Storage) — none found beyond ANH. ANH and ACS are the complete list,
and both can be provisioned whenever, independent of getting the pipeline itself working.

### Azure Notification Hub — provisioning checklist

1. A **Notification Hub Namespace** (Free or Basic tier is plenty for dev traffic).
2. A **Notification Hub** inside that namespace.
3. **Platform credentials registered on the hub** — only Windows (WNS) matters today, since MAUI
   Windows is the only buildable/testable target (per `docs/client-sdk.md`): a Microsoft Entra app
   registration (Package SID + client secret) tied to the app's Windows packaging identity. FCM
   (Android)/APNs (iOS) can be deferred — the notifier's payload-building code already handles
   those formats, there's just no real device to test against yet.
4. Take the namespace's connection string (ideally a dedicated SAS policy scoped to Listen+Send,
   not the full-access default) and the hub's name → these become
   `Azure:NotificationHub:ConnectionString` / `Azure:NotificationHub:Name` in each environment's
   config (a GitHub Environment secret once the actual pipeline exists, a user-secret/env var for
   now).
5. **Caveat**: this alone doesn't make push notifications work end-to-end. `RegisterPushDeviceAsync`
   in the MAUI client currently sends a locally-generated placeholder token (no real FCM/APNs/WNS
   SDK wired up yet, per `CONTEXT.md`) — provisioning ANH now gets the server-side infra ready, but
   the MAUI-side integration is a separate, not-yet-scheduled piece of work.

### Getting secrets into the Compose deploy

No Key Vault, no Azure authentication step — `deploy-develop.yml`/`deploy-personal.yml` read their
secrets directly from that branch's GitHub Environment (`secrets.IDENTITY_DB`,
`secrets.NOTIFICATION_HUB_CONNECTION_STRING`, etc., once those exist) and export them as the env
vars `aspire deploy`'s Docker Compose target (or a plain `docker compose up`) expects. Simpler than
the Key Vault path this replaced, at the cost of secrets living in GitHub rather than a dedicated
secrets-management service — an acceptable tradeoff for two environments you fully control.

### Azure Communication Services — provisioning checklist

1. A **Communication Services** resource (the parent resource).
2. An **Email Communication Services** resource, linked to it.
3. A **sending domain** — either the free Azure-managed subdomain (`<something>.azurecomm.net`,
   no DNS verification needed) or a custom domain (needs TXT/DKIM/SPF records at your registrar) —
   your call, you've got DNS control covered either way.
4. Take the resource's connection string.
5. **Not just infra**: unlike ANH, there's no code today that consumes ACS. Someone needs to write
   an ACS-based `INotificationService` implementation (alongside or replacing `SmtpNotificationService`)
   before this resource does anything — flag that as a small follow-up task when this is picked up.

### Shared or per-environment resources? — decided

**Split by cost**: the free-tier resource gets isolated, the one that costs real money gets shared.

- **Azure Notification Hub — separate per environment** (`develop` gets its own namespace/hub,
  `personal` gets its own). Free/Basic tier, so isolation costs nothing extra — a `develop` test
  run's push traffic never touches `personal`, or vice versa.
- **Azure Communication Services — one shared resource** across `develop` and `personal`. Costs
  real money per email sent, so one resource keeps that to a single bill; the two environments stay
  distinguishable by sender address/display name rather than by separate resources.

(Key Vault dropped from the design entirely — see above — so no sharing question applies to it.
GitHub Environment secrets are inherently separate per environment already, `develop`'s and
`personal`'s own Environments each hold their own copies.)

## Log repository for `develop`/`personal`

For now: **a local folder on the home PC**, not a real log-aggregation service — matches the low
expected activity on these two environments. Two ways to get there, different amounts of work:

- **Zero-code (recommended for now)**: rely on Docker's own log capture. Set the `json-file` log
  driver's rotation options (`max-size`, `max-file`) per service in the Compose output — Aspire's
  per-resource Docker Compose customization API (exact method TBD at implementation time, same
  "confirm via `aspire docs api search`" caveat as elsewhere in this doc) can set this. Logs stay
  retrievable anytime via `docker compose logs`/Docker Desktop's own UI — no app code changes, no
  new dependency. The one caveat: with Docker Desktop's WSL2 backend, the underlying log files live
  inside the WSL2 VM's filesystem, not a plain Windows path you can browse in Explorer directly.
- **Real browsable files**: add a file-logging sink (e.g. `Serilog.Sinks.File`, not currently a
  dependency anywhere in the repo — this would be a small new addition to `Server`/`IdentityPlatform`
  `Program.cs`) writing into a container path, bind-mounted to a real Windows folder (e.g.
  `C:\IdentityLogs\develop\`, `C:\IdentityLogs\personal\`) via the Compose service definition. More
  setup, but gives you an actual folder you can open in Explorer.

Recommendation: start with the zero-code option given the "low activity" framing — revisit if
`docker compose logs` turns out to be too inconvenient in practice.

## Findings behind the design

- **Fully greenfield for CI/CD**: `.github/workflows/` is empty, no Dockerfiles, no Helm/Bicep, no
  `appsettings.Production.json`, no versioning scheme anywhere in the repo.
- **`IdentityDb`** is already an `AddConnectionString` parameter in `AppHost.cs` — not an
  Aspire-provisioned database. The app already expects an externally-supplied connection string in
  every environment, dev included, so this pipeline doesn't need to change that model, just supply
  a different value per environment.
- **`Redis` removed from `AppHost.cs`** (done). History check confirmed the intent: commit
  `4902944` ("client ID cache with PeriodicTimer, Redis in Aspire") added it specifically to back
  client/session lookups, but the actual implementation (`ClientIdCache.cs`) ended up as a plain
  in-memory `PeriodicTimer` cache — Redis was never wired into any consumer, in any project, from
  that commit onward. Removed the `AddRedis("Redis")` resource/reference and the
  `Aspire.Hosting.Redis` package from `AppHost.csproj`; `dotnet build` confirmed clean. No
  per-environment Azure Cache/Helm plumbing needed for this pipeline as a result. If a real
  distributed-cache need shows up later, it can be re-added with the same per-environment treatment
  as everything else at that point.

## Quality Gate tooling — decided (phase 1)

**Decided**: start with the zero-cost option — a coverage-threshold check plus CodeQL — and
revisit SonarCloud later once there's a reason to pay for/operate more than that.

- **Coverage threshold in CI** — the repo already produces coverage via ReportGenerator
  (`TestResults\html`, see `CONTEXT.md`'s test suite bullet). Add a CI step that runs the test
  suite with `coverlet` and fails the build if line/branch coverage drops below a threshold (start
  from roughly the current baseline — 418/418 unit tests passing today — rather than an arbitrary
  number; tighten over time). Also tighten the existing Roslyn analyzer config
  (`Directory.Build.props` already sets `AnalysisMode`/`EnforceCodeStyleInBuild`) if it isn't
  already at its strictest useful setting. Zero new services, runs on every PR into `develop`.
- **CodeQL** — free, GitHub-native security static analysis (`github/codeql-action`), scheduled
  scan + PR-triggered scan, surfaces results as code-scanning alerts. Near-zero setup, complements
  the coverage gate (different concern: known vulnerability patterns vs. regressions/dead paths).

**Deferred, not rejected**: SonarCloud (or self-hosted SonarQube) stays the phase-2 option once the
zero-cost gate's limits are actually felt — no coverage/duplication trend over time, no
maintainability metric, no dedicated dashboard. Revisit then rather than now.

## Open items

**Resolved this round:**

- ~~Coverage-threshold/CodeQL gate placement~~ → **`develop` PRs only**, since promotion merges
  carry no independent code changes.
- ~~Shared or per-environment ANH/ACS~~ → **ANH separate per environment** (free tier); **ACS
  shared** across `develop`/`personal` (costs real money to run twice). Key Vault later dropped from
  the design entirely, superseded by GitHub Environment secrets — see "Secrets storage" note below.
- ~~Where `develop`/`personal` secrets live~~ → **GitHub Environment secrets** (`develop` and
  `personal` Environments, created with no required reviewer so they stay ungated) — not Azure Key
  Vault as first proposed. Simpler, no Azure login needed for these two branches at all, at the cost
  of secrets sitting in GitHub rather than a dedicated secrets service. Also corrected: the actual
  home PC for `develop`/`personal` is **Ubuntu**, not Windows as first assumed — the self-hosted
  runner setup and `deploy-develop.yml`/`deploy-personal.yml` target Linux accordingly.
- ~~Tag creation process~~ → **automatic**, via `promote.yml` for app deploy tags (date+run-number
  scheme), and via **release-please** for `Client.Core`/`Client.Blazor`/`Client.Maui`/`Client.Wpf`
  (real semver from Conventional Commits).
- ~~Rollback~~ → **republish the previous tag**, via `workflow_dispatch` on the relevant deploy
  workflow — see "Rollback" above.
- ~~`docs` environment naming~~ → clarified: it's genuinely public, hosting the IDP's API
  documentation for third-party integrators — see the note under "Branch chain and environments."
- ~~Which branch feeds release-please~~ → **`develop`**, decoupling package/app release cadence from
  the app's own promotion cascade — see "Client package/app versioning" above.
- ~~`Client.Maui`/`Client.Wpf` distribution target~~ → **GitHub Releases**, not the Microsoft
  Store/Google Play — see "`Client.Maui` distribution — decided" above.

**Still open:** none right now — every decision point raised so far has been resolved. This doc is
ready to hand off to implementation; re-open a new "Open items" entry if something else surfaces
while building it.

## Implementation plan (once the open items above are resolved)

Not scheduled yet — this is the ordered breakdown of what's left, split into what only touches the
repo versus what needs your Azure/GitHub access. `docs`/`integration`/`production` code paths are
included since they cost nothing extra to write alongside `develop`/`personal`'s, but they won't be
*exercised* until their Azure infrastructure exists later (out of scope for now, per your call).

**Repo-only (no external access needed):**

1. Re-parent `personal` onto `develop` (git branch surgery, force-push to `origin/personal`).
2. `AppHost.cs`: add the `deploy-target` parameter and the per-target `switch` (Redis removal
   already done, see "Findings" above).
3. `.github/workflows/_deploy.yml` (reusable) + the five thin trigger workflows
   (`deploy-develop.yml`, `deploy-personal.yml`, `deploy-docs.yml`, `deploy-integration.yml`,
   `deploy-production.yml`, each with `workflow_dispatch` for rollback) + `promote.yml` (the
   promotion cascade, including auto-tagging).
4. Coverage-threshold + CodeQL workflows (Quality Gate phase 1) on `develop` PRs.
5. `release-please.yml` (triggered on push to `develop`) + its manifest config for the four
   `clients/*` components.
6. `publish-client-maui.yml` (triggered on `client-maui-v*` tags) — build Windows unpackaged zip +
   signed MSIX + Android APK, publish a GitHub Release with all three attached.
7. `CONTEXT.md`: update the forward-merge description to the five-branch chain, and add the `docs`
   environment clarification (genuinely public API docs, not just a stage name).

**Needs your Azure/GitHub access, can happen in parallel with the above:**

8. Self-hosted runner registered on the home PC — **Ubuntu/Linux setup**, not Windows (corrected).
9. `develop` and `personal` GitHub Environments created (no required reviewer) + their secrets
   populated (at minimum `Parameters__IdentityDb`; `Azure:NotificationHub:ConnectionString`/`Name`
   and the ACS connection string once those resources/code exist).
10. Azure Notification Hub (separate per environment) + Communication Services (shared, once its
    consuming code exists) for `develop` and `personal`, per the checklists above — optional, not
    blocking the first deploys (both no-op/fall back gracefully when unconfigured).
11. GHCR package visibility confirmed for whatever images `develop`/`personal` pull.
12. Self-signed Windows code-signing certificate generated, exported, and stored as the two
    `WINDOWS_SIGNING_CERT_*` repository secrets — see "`Client.Maui` distribution" above.

**Small code follow-up, not part of the pipeline itself:**

13. An ACS-based `INotificationService` implementation, once the ACS resource exists (item 10) —
    today only `SmtpNotificationService` exists.

**Explicitly not part of this pass:** anything for `docs`/`integration`/`production`'s own Azure
infrastructure (App Service, the two AKS clusters, their OIDC app registrations/resource groups) —
you're not provisioning those yet, so items 8-12 above are scoped to `develop`/`personal` only
(item 12, the signing certificate, is really `Client.Maui`-scoped rather than environment-scoped,
but grouped here since it's the same "needs your access outside the repo" category).
