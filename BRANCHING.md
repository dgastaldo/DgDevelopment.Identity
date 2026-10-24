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
- When work is complete, mark the PR as **ready for review** (`gh pr ready`). Do not leave it as Draft.

## Workflows

### Feature Development

```bash
# 1. Create feature branch from develop
git checkout develop
git pull
git checkout -b feature/oauth-authorize-endpoint

# 2. Make an initial commit (scaffold, TODO, or first change)
git add .
git commit -m "feat: scaffold authorize endpoint"

# 3. Push and create PR immediately (use Draft if work is in progress)
git push -u origin feature/oauth-authorize-endpoint
gh pr create --base develop --head feature/oauth-authorize-endpoint --title "feat: add authorize endpoint" --body "..." --draft

# 4. Continue developing and committing
git add .
git commit -m "feat: implement authorization code validation"
git push

# 5. Mark PR as ready for review
gh pr ready

# 6. When approved, squash merge to develop
# GitHub auto-deletes branch after merge
```

> **Important**: The first commit and PR must be created **as soon as possible** (ideally within minutes of branch creation). Create as Draft if work is incomplete. When all work is done, run `gh pr ready` to mark it ready for review.

### Promotion to Upper Branches

The version tag is cut on `docs` (when content is documented) and travels forward through the chain.

```bash
# Promote develop -> docs (cut the version tag here)
git checkout docs
git pull
git merge develop
git tag v0.1.0
git push --atomic origin docs v0.1.0

# Promote docs -> integration (tag travels with the merge)
git checkout integration
git pull
git merge docs
git push --atomic origin integration v0.1.0

# Promote integration -> main (release — tag travels to production)
git checkout main
git pull
git merge integration
git push --atomic origin main v0.1.0
```

### Hotfix (Production Fix)

Hotfix tag is cut on `main` (it's a production fix), then travels backward through the chain.

```bash
# 1. Create hotfix from main
git checkout main
git pull
git checkout -b fix/critical-token-bug

# 2. Fix and commit
git add .
git commit -m "fix: critical token validation bug"

# 3. PR -> squash merge into main, tag here
git push -u origin fix/critical-token-bug
# Create PR targeting main -> squash merge

# 4. Tag release on main
git checkout main
git pull
git tag v0.1.1
git push --atomic origin main v0.1.1

# 5. Backport to all branches (tag travels backward)
git checkout integration
git merge main
git push --atomic origin integration v0.1.1

git checkout docs
git merge integration
git push --atomic origin docs v0.1.1

git checkout develop
git merge docs
git push --atomic origin develop v0.1.1
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
- Tags are cut on `docs` (normal release) or `main` (hotfix) and travel with the code through all branches.
- A given tag `v1.0.0` exists on all branches: `develop` → `docs` → `integration` → `main`.
- Pre-release tags for integration testing: `v1.0.0-rc1`.
