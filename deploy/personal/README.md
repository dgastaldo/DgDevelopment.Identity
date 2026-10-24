# Personal deploy (Watchtower, pull-based)

No GitHub Actions runner lives on the NUC. The pipeline (`05-deploy-personal.yml`, GitHub-hosted
`ubuntu-latest`) only builds `identity-server`/`identity-platform` container images and pushes them
to GHCR as `ghcr.io/dgastaldo/dgdevelopment-identity-server:personal-latest` and
`ghcr.io/dgastaldo/dgdevelopment-identity-platform:personal-latest`. `identity-watchtower`, running
on the NUC, checks GHCR every 5 minutes and recreates the containers when a new `personal-latest`
digest appears.

`identity-watchtower` runs with `--run-once`: each check is a single pass that exits 0 immediately
after - same as e.g. Nextcloud AIO's watchtower, it sits `Exited (0)` between checks instead of
staying `Up` continuously. `identity-watchtower-scheduler` (a tiny `ofelia` container) is what
restarts it every 5 minutes; it's the one long-running/`Up` container of the pair. Watchtower's own
config (volumes, env vars) lives entirely on the `identity-watchtower` service above - the scheduler
only ever runs `docker start identity-watchtower` on a schedule, it doesn't recreate or duplicate
Watchtower's setup.

`containrrr/watchtower` is unmaintained (archived upstream) and its embedded Docker client hardcodes
API version 1.25, which a Docker Engine new enough to have dropped that version rejects
(`client version 1.25 is too old`) - `DOCKER_API_VERSION` pins it to the oldest version still
accepted instead. If a future Docker Engine update moves that floor again, bump the value to match
the error message's "Minimum supported API version" - or, longer term, switch to a maintained fork.

`admin-client-config-init` fixes a fresh `admin-client-config` volume's default root ownership
before `identity-server` (which runs as a non-root user) ever needs to write to it - without it,
first seed or `rotate-admin-client-secret` fails with `UnauthorizedAccessException: Access to the
path '/shared/admin-client-config.json' is denied`.

## One-time NUC setup

1. Install Docker + the Compose plugin if not already present, and make sure your user can run
   `docker` without `sudo` (`sudo usermod -aG docker $USER`, then re-login).
2. Create a GitHub fine-grained PAT scoped to this repo with **read:packages** only, and log in once:
   ```bash
   docker login ghcr.io -u dgastaldo -p <PAT>
   ```
   This writes credentials to `~/.docker/config.json` - both `docker compose pull` and
   `identity-watchtower` (mounted read-only) reuse this same file. The PAT needs no expiry-renewal automation: it's only
   read once by `docker login`, not re-checked afterwards, but rotate it periodically like any
   long-lived credential.
3. Copy `docker-compose.yml` and `.env.example` (renamed to `.env`) from this folder onto the NUC.
   Fill in `.env` with real values - in particular `GHCR_DOCKER_CONFIG_FILE` should point at the
   `~/.docker/config.json` written in step 2, and `IDENTITY_SERVER_PUBLIC_URL`/
   `IDENTITY_PLATFORM_PUBLIC_URL` should be the NUC's actual LAN address (e.g. `http://192.168.1.5:8081`
   / `:8082`), not `localhost` - the browser, not the NUC itself, is what follows these redirects.
   `identity-server` also needs `IDENTITY_PLATFORM_PUBLIC_URL`'s value (wired through as
   `AdminPlatformBaseUrl`) so the seeded admin OAuth client's redirect URI matches wherever
   `identity-platform` is actually reached from - without it, login fails with "Invalid
   redirect_uri" regardless of what `IDENTITY_SERVER_PUBLIC_URL` is set to, since the two problems
   are independent (see `DbSeeder.EnsureAdminClientRedirectUriAsync`).
4. First run:
   ```bash
   docker compose pull
   docker compose up -d
   ```
   `identity-server` migrates its database, seeds a *random* admin OAuth client (unique to this
   environment, not something you choose), and writes its id/secret to the `admin-client-config`
   volume shared with `identity-platform` - which reads that file automatically at startup, no
   manual copying. The only race to be aware of: if `identity-platform` happens to start before
   `identity-server` finishes its first seed, it just won't have the file yet - one
   `docker compose restart identity-platform` once you've confirmed the server has seeded
   (`docker compose logs identity-server`, look for "Admin client config for identity-platform
   written to") fixes it. Same logs also show the seeded superadmin *user* password
   (`SUPERADMIN CREDENTIALS` block) - that one really is only ever in the logs, not recoverable
   afterwards other than resetting the user.

### Rotating the admin client secret later

```bash
docker compose run --rm identity-server dotnet DgDevelopment.Identity.Server.dll rotate-admin-client-secret
docker compose restart identity-platform
```

Generates a fresh secret, updates it in the database, and rewrites the same shared config file -
`identity-platform` picks it up on that restart. `ClientId` itself never changes (it's the OAuth
client's business key); only the secret rotates.

From here on, merging a promotion PR into `personal` (or running `05-deploy-personal.yml` via
`workflow_dispatch`) builds new images tagged with each app's current release-please version (from
`.release-please-manifest.json`) and re-points `personal-latest` at them; `identity-watchtower` picks
up the new `personal-latest` digest on its own within one poll interval, no further action needed on
the NUC.

## Rollback

Edit the two `image:` tags in `docker-compose.yml` from `:personal-latest` to a specific
`:personal-X.Y.Z` version (visible in the GHCR package's tag list - matches the app's release-please
version at the time it was deployed), then `docker compose up -d`. `identity-watchtower` will leave a
pinned version tag alone since it only watches `personal-latest`'s digest.

## What this doesn't cover yet

- No TLS/reverse proxy in front of the two apps - they're plain HTTP on `8081`/`8082`. Fine for a
  home LAN; put a reverse proxy (Caddy/nginx) in front before exposing this beyond your LAN.
- No automated backup of the `sql-data` volume.
