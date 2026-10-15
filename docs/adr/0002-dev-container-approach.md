# ADR 0002: Dev Container and Vendored Skills Approach

## Status

Accepted — 2026-10-15

## Context

**Primary Goal**: A second developer must be able to:
1. Clone the repository
2. Open it in a dev container
3. Add their own tokens (`.env`)
4. Start working immediately

**No manual tool installs.** Everything needed must be pinned and reproducible.

**Challenge**: Different developers use different AI agents (Claude Code, Cline, Windsurf, Cursor, Antigravity). How do we provide agent skills that work across all tools?

## Decision

### Part 1: Dev Container

Use a **VS Code Dev Container** with pinned tool versions:

- **Base image**: `mcr.microsoft.com/devcontainers/dotnet:8.0`
- **Features**: Node LTS, Docker-outside-of-docker, GitHub CLI
- **Manual installs** (postCreateCommand): pnpm (via corepack), Terraform, Claude Code CLI
- **Recommended extensions**: C# Dev Kit, ESLint, Prettier, Docker, Terraform
- **Bootstrap**: `postCreateCommand: bash scripts/bootstrap.sh`

**Rationale**:
- ✅ **Reproducible**: Same tools, same versions, every developer
- ✅ **Cross-platform**: Works on Windows (WSL), macOS, Linux
- ✅ **Isolated**: No "works on my machine" issues
- ✅ **Fast onboarding**: One-click "Reopen in Container"

### Part 2: Vendored Skills

**Vendor** AI agent skills into `.agents/skills/`:

- Download Superpowers + skill-creator from https://github.com/coleam00/ai-agent-superpowers
- Place in `.agents/skills/` (committed to repo)
- Record provenance in `.agents/skills/SOURCES.md` (source URL, commit hash, license)
- Symlink `.claude/skills` and `.agent/skills` to `.agents/skills`

**Rationale**:
- ✅ **Version lock**: Skills are frozen at a known-good state
- ✅ **Offline-first**: No external downloads during onboarding
- ✅ **Multi-agent**: Both Claude Code and Antigravity can use them
- ✅ **Auditable**: `SOURCES.md` tracks exactly what we're running

**Windows fallback**: If symlinks fail (common on Windows without Developer Mode):
- Create `scripts/sync-skills.ps1` to copy `.agents/skills/` → `.claude/skills/` and `.agent/skills/`
- Run sync script in bootstrap

### Part 3: Health Checks

**`scripts/doctor.sh`** validates the environment before committing:

1. Tool versions: `dotnet --version` (8.0.x), `node --version` (20.x), `pnpm --version`, `terraform --version`, `docker --version`
2. Docker running: `docker ps` succeeds
3. `.env` exists with required keys: `GITHUB_TOKEN`, `NOTION_TOKEN`
4. Compose infra healthy: `docker compose -f deploy/compose/docker-compose.yml ps` all services show "healthy"
5. MCP config valid: `.mcp.json` parses as valid JSON, no inline secrets
6. Skills present: `.agents/skills/` exists with `SOURCES.md`

**Exit 1 on any failure**, print clear fix instructions.

## Consequences

### Positive

- ✅ **Zero manual tool installs**: Clone → open in container → work
- ✅ **Consistent tooling**: No version mismatches, no "upgrade Node" debugging
- ✅ **Clear health checks**: `doctor.sh` catches misconfigurations before CI
- ✅ **Multi-agent ready**: Works with Claude Code, Cline, Antigravity out of the box
- ✅ **Offline-capable**: Vendored skills don't require internet after first clone

### Negative

- ❌ **Dev container image size**: ~2-3 GB (SDK + Node + tools)
  - Mitigated by: Docker layer caching, incremental rebuilds
- ❌ **Windows symlink limitations**: Requires Developer Mode or fallback sync script
  - Mitigated by: Bootstrap detects failure, uses PowerShell sync
- ❌ **Skill staleness**: Vendored skills don't auto-update
  - Mitigated by: Periodic manual reviews, documented update process

### Alternatives Considered

#### Alternative 1: Package manager install scripts

**Rejected** because:
- ❌ Relies on external package managers (apt, brew, choco) being configured
- ❌ Version drift (developer installs "latest" at different times)
- ❌ No isolation (tools pollute host system)

#### Alternative 2: Dynamically fetch skills at startup

**Rejected** because:
- ❌ Internet dependency (fails in air-gapped environments)
- ❌ No version lock (upstream changes can break our workflows)
- ❌ Auditing/security: unclear what code is running

#### Alternative 3: Docker Compose instead of Dev Container

**Rejected** because:
- ❌ No IDE integration (extensions, debugger, IntelliSense)
- ❌ Complex volume mounts for cross-platform
- ❌ Worse developer experience (can't "F5 debug")

Dev Containers offer the best balance of isolation, reproducibility, and DX.

## Implementation

### Dev Container Setup

File: `.devcontainer/devcontainer.json`

```json
{
  "name": "ShopSphere",
  "image": "mcr.microsoft.com/devcontainers/dotnet:8.0",
  "features": {
    "ghcr.io/devcontainers/features/node:1": { "version": "20" },
    "ghcr.io/devcontainers/features/docker-outside-of-docker:1": {},
    "ghcr.io/devcontainers/features/github-cli:1": {}
  },
  "postCreateCommand": "bash scripts/bootstrap.sh",
  "customizations": {
    "vscode": {
      "extensions": [
        "ms-dotnettools.csdevkit",
        "dbaeumer.vscode-eslint",
        "esbenp.prettier-vscode",
        "ms-azuretools.vscode-docker",
        "hashicorp.terraform"
      ]
    }
  },
  "forwardPorts": [5000, 5001, 5002, 1433, 5672, 15672, 6379]
}
```

### Bootstrap Script

File: `scripts/bootstrap.sh`

```bash
#!/usr/bin/env bash
set -e

echo "🔧 Enabling corepack for pnpm..."
corepack enable

echo "📦 Restoring .NET workload..."
dotnet restore || true

echo "📦 Installing pnpm dependencies (when applicable)..."
if [ -f "package.json" ]; then
  pnpm install
fi

echo "🔗 Creating skill symlinks..."
if [ ! -L ".claude/skills" ]; then
  ln -s ../.agents/skills .claude/skills || {
    echo "⚠️  Symlink failed, using sync script fallback"
    pwsh scripts/sync-skills.ps1
  }
fi

if [ ! -L ".agent/skills" ]; then
  ln -s ../.agents/skills .agent/skills || {
    echo "⚠️  Symlink failed, using sync script fallback"
    pwsh scripts/sync-skills.ps1
  }
fi

echo "✅ Bootstrap complete! Run 'bash scripts/doctor.sh' to verify."
```

### Skill Provenance

File: `.agents/skills/SOURCES.md`

```markdown
# Vendored Skills Provenance

## Superpowers
- **Source**: https://github.com/coleam00/ai-agent-superpowers
- **Commit**: [insert commit hash]
- **License**: MIT
- **Date Vendored**: 2026-10-15
- **Reason**: Cross-agent skill library for common workflows

## skill-creator
- **Source**: https://github.com/coleam00/ai-agent-superpowers
- **Commit**: [insert commit hash]
- **License**: MIT
- **Date Vendored**: 2026-10-15
- **Reason**: Scaffold new skills in standardized format

## Update Process

1. Review upstream changes: `git log --since="2026-10-15" -- .`
2. Test changes in isolated branch
3. Update this file with new commit hash
4. Run `bash scripts/doctor.sh` to validate
5. Commit updated skills + updated `SOURCES.md`
```

## Verification

After setup, run:

```bash
# In dev container
bash scripts/doctor.sh
```

Should see:
```
✅ dotnet 8.0.404
✅ node v20.18.0
✅ pnpm 9.12.2
✅ terraform 1.9.8
✅ docker 27.3.1
✅ Docker is running
✅ .env exists with required keys
✅ Compose infra healthy
✅ .mcp.json valid
✅ Skills present

🎉 Environment ready!
```

## References

- [VS Code Dev Containers](https://code.visualstudio.com/docs/devcontainers/containers)
- [Dev Container Images](https://github.com/devcontainers/images)
- [Superpowers Repository](https://github.com/coleam00/ai-agent-superpowers)
- [Implementation Files](../../.devcontainer/devcontainer.json)

## Review

- **Revisit**: When new dev container features become available, or when skill management patterns evolve
- **Owner**: Platform team
