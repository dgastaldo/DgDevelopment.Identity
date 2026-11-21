# Infrastructure as Code — Azure, via Terraform

Supersedes the original plan (in `docs/deployment.md`'s "Aspire-native deployment model") to let
`AppHost.cs` provision `AddAzureKubernetesEnvironment`/`AddAzureAppServiceEnvironment` itself for
`integration`/`main`/`docs`. **Decided instead**: Terraform owns every Azure resource this project
needs, across every environment; Aspire/`aspire deploy` is no longer used to provision anything —
it stays only for `personal`/`develop`'s Docker Compose stack (already working, not Azure, nothing
to change there). For `docs`/`integration`/`production`, the app itself is deployed by a plain
build-image → push-to-GHCR → `az webapp config container set` / `kubectl set image` step, the same
shape `05-deploy-personal.yml` already uses for `personal` — not written yet, tracked separately
from this IaC layer. `AppHost.cs` never grows the `docs`/`integration`/`main` cases that were
sketched in `docs/deployment.md`; that section of the doc needs a follow-up edit to reflect this.

## Layout

```
infra/terraform/
  modules/              # reusable building blocks, one Azure concept each
    resource-group/
    notification-hub/   # push MFA (Azure Notification Hub)
    communication-services/  # email (Azure Communication Services) - not consumed by app code yet
    sql/                 # Azure SQL server + IdentityDb database
    app-service/         # docs' Linux Web App x2 (Server + IdentityPlatform), GHCR-sourced containers
    aks/                 # integration/production's AKS cluster
    ghcr-pull-secret/    # k8s imagePullSecret for GHCR, used by aks environments
  environments/
    personal/   # RG + its own Notification Hub only - personal itself stays on the NUC
    shared/     # RG + one Communication Services resource, shared by develop + personal
    docs/       # RG + SQL + Notification Hub + App Service
    integration/# RG + SQL + Notification Hub + AKS + GHCR pull secret
    production/ # same shape as integration, fully separate resource group/cluster
```

`develop` has no environment folder of its own: its ephemeral test stack is Docker Compose on a
GitHub-hosted runner, same as `personal`, and its own Notification Hub namespace is explicitly
deferred per `docs/deployment.md` ("`develop`'s ANH namespace can wait") — add one when the UI-level
push-MFA E2E test that needs it actually gets written.

## One-time manual bootstrap (cannot be done by Terraform itself)

1. **State backend storage account** — Terraform's own state has to live somewhere before Terraform
   can run. Create by hand (Azure Portal or `az cli`, once): a resource group
   `dgdevelopment-identity-tfstate`, a Storage Account inside it (name must be globally unique —
   `dgdevidentitytfstate` is a placeholder, adjust in every `versions.tf` if taken), and a blob
   container `tfstate` inside that account. Each environment gets its own state file (`key =
   "<env>.tfstate"`) in the same container — no cross-environment locking conflicts.
2. **CI → Azure OIDC identity** — one Azure AD App Registration (Entra ID → App registrations →
   New registration), with a **Federated Credential** scoped to this repo + the branch that runs
   `terraform apply` for each environment (GitHub Actions OIDC, subject
   `repo:dgastaldo/DgDevelopment.Identity:ref:refs/heads/<branch>` — one federated credential per
   branch: `personal`, `docs`, `integration`, `main`, plus one for `develop`/pull_request if `plan`
   should run on PRs too). Grant it:
   - **Storage Blob Data Contributor** on the tfstate storage account (to read/write state).
   - **Contributor** on each resource group it manages (or on the subscription, if you'd rather not
     pre-create empty resource groups by hand — Terraform's own `resource-group` module creates
     them, so subscription-level Contributor is simplest for a first pass).
   Record `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` as GitHub Environment
   secrets/variables per environment (same Environments `docs`/`integration`/`production` already
   used for Aspire's OIDC login — this is the same identity shape, just also used for Terraform
   now, so it can likely be the *same* App Registration `docs/deployment.md` already called for,
   with its federated credential's role grants widened to cover what Terraform now provisions).
3. **GHCR credentials** — a PAT with `read:packages` (same one `docs/deployment.md`'s "Manual,
   out-of-band setup" step 4 already calls for), passed to Terraform as `TF_VAR_ghcr_username` /
   `TF_VAR_ghcr_token`.
4. **SQL admin password**, per environment — generate and store as `TF_VAR_sql_admin_password` in
   that environment's GitHub Environment secrets. Consider migrating to Azure AD-only SQL auth
   later (removes this secret entirely).
5. **FCM / APNs credentials**, once you have a real Firebase project (see `CONTEXT.md`'s push-MFA
   gap) — `TF_VAR_fcm_api_key` per environment that should have Android push working.

## Running locally

```bash
cd infra/terraform/environments/<env>
terraform init
terraform plan   -var-file=... # or TF_VAR_ environment variables, same as CI
terraform apply
```

Needs `az login` (or the same OIDC federated credential, via `az login --federated-token`) with
access to the subscription, plus the `TF_VAR_*` secrets above set as environment variables.
**Not run or validated from this session** — no Azure credentials or Terraform CLI available in
this environment; review before applying for real.

## Known caveats — read before applying

- **`notification-hub` module's FCM credential**: `azurerm_notification_hub`'s `gcm_credential`
  block only accepts the legacy FCM server key (`api_key`), not FCM v1 service-account credentials.
  `docs/deployment.md`'s checklist calls for FCM v1. Verify against the current `azurerm` provider
  docs before relying on this — the legacy server key (enable "Cloud Messaging API (Legacy)" in the
  Firebase console if needed) is the fallback if v1 truly isn't supported by the resource yet.
- **`integration`/`production`'s `kubernetes` provider**: it's configured from `module.aks`'s own
  output (so no separate `az aks get-credentials` step is needed), but this means the *first*
  `terraform apply` on a brand-new environment may need to run twice — the provider can't connect
  to a cluster that doesn't exist yet on the very first plan. If `terraform apply` fails on a fresh
  environment with a provider-connection error, run it again (the AKS cluster from the first,
  partially-applied run will already exist).
- **App Service + GHCR**: GHCR has no native Azure AD/managed-identity federation (unlike ACR), so
  `app-service`'s two Linux Web Apps authenticate to `ghcr.io` with a plain username/PAT via
  `DOCKER_REGISTRY_SERVER_*` app settings — rotate that PAT the same way the AKS pull secret's PAT
  gets rotated.
- **SQL firewall**: the `sql` module opens `AllowAzureServices` (0.0.0.0/0.0.0.0 in Azure's own
  firewall-rule convention, which actually means "any Azure-hosted service," not public internet) —
  narrow this to the compute environment's real egress IPs once they're stable, if you want tighter
  isolation than that.
- **No `terraform fmt`/`validate`/`init` run against these files** — written without a Terraform
  CLI or Azure credentials available in this session. Run `terraform fmt -recursive` and
  `terraform validate` (per environment, after `init`) before the first real `plan`.
