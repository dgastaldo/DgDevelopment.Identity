# Branching Strategy — DgDevelopment.Identity

## Branch Model

```
main ─────────────────────────────────────── Production
  ▲
  │ (merge forward / merge backward)
integration ──────────────────────────────── Third-party integration testing
  ▲
  │ (merge forward / merge backward)
docs ─────────────────────────────────────── API documentation + generated OpenAPI
  ▲
  │ (merge forward / merge backward)
develop ──────────────────────────────────── Active development
  │         ▲
  │ PR + squash
  ▼         │
feature/*   fix/*
```

## Branch Descriptions

| Branch | Purpose | Direct commits | Receives |
|---|---|---|---|
| `main` | Production code | Never | Merges from `integration` |
| `integration` | Third-party integration testing | Never | Merges from `docs` (forward) or `main` (backward) |
| `docs` | API documentation + generated OpenAPI | Never | Merges from `develop` (forward) or `integration` (backward) |
| `develop` | Active development, base for all features | PR squash merge only | Squash merges from `feature/*` and `fix/*` |
| `feature/*` | New features | Yes | — |
| `fix/*` | Bug fixes | Yes | — |

## Naming Convention

```
feature/<short-description>
fix/<short-description>
```

Kebab-case, in English. Examples:
- `feature/oauth-authorize-endpoint`
- `feature/user-email-aliases`
- `fix/login-lockout-bug`
- `fix/token-expiration-validation`

## Commit Convention

```
<type>: <short description>
```

| Type | Use |
|---|---|
| `feat` | New feature |
| `fix` | Bug fix |
| `docs` | Documentation changes |
| `refactor` | Code refactoring (no functional change) |
| `test` | Adding or modifying tests |
| `chore` | Build, CI/CD, dependencies, tooling |
| `perf` | Performance improvements |
| `style` | Code style, formatting (no logic change) |

Examples:
```
feat: add OAuth authorize endpoint
fix: prevent refresh token replay attack
docs: add branching strategy document
refactor: extract token validation to dedicated service
test: add integration tests for device code flow
chore: update EF Core to 10.0
```

## Pull Request Policy

- All merges into `develop` require a **Pull Request**.
- **1 approval** required from a reviewer.
- Squash merge only — single clean commit per feature.
- Branch deleted after merge.

## Workflows

### Feature Development

```bash
# 1. Create feature branch from develop
git checkout develop
git pull
git checkout -b feature/oauth-authorize-endpoint

# 2. Develop and commit
git add .
git commit -m "feat: add authorize endpoint"

# 3. Push and create PR
git push -u origin feature/oauth-authorize-endpoint
# Create PR on GitHub targeting develop
# Get 1 approval, squash merge

# 4. Delete feature branch (GitHub auto-deletes after merge)
```

### Promotion to Upper Branches

```bash
# Promote develop -> docs
git checkout docs
git pull
git merge develop
git push

# Promote docs -> integration
git checkout integration
git pull
git merge docs
git push

# Promote integration -> main (release)
git checkout main
git pull
git merge integration
git tag v0.1.0
git push --atomic origin main v0.1.0
```

### Hotfix (Production Fix)

```bash
# 1. Create hotfix from main
git checkout main
git pull
git checkout -b fix/critical-token-bug

# 2. Fix and commit
git add .
git commit -m "fix: critical token validation bug"

# 3. PR -> squash merge into main
git push -u origin fix/critical-token-bug
# Create PR targeting main -> squash merge

# 4. Tag release
git checkout main
git pull
git tag v0.1.1
git push origin v0.1.1

# 5. Backport to all branches
git checkout integration
git merge main
git push

git checkout docs
git merge integration
git push

git checkout develop
git merge docs
git push
```

### Syncing Branches (Bidirectional)

Forward sync (promotion):
```
develop -> docs -> integration -> main
```

Backward sync (hotfix backport):
```
main -> integration -> docs -> develop
```

Regular sync is encouraged to keep branches aligned. A stale `integration` or `docs` branch should be avoided.

## Versioning

- Semantic versioning: `MAJOR.MINOR.PATCH`
- Tags on `main` branch only: `v1.0.0`, `v1.1.0`
- Pre-release tags for integration: `v1.0.0-rc1`
