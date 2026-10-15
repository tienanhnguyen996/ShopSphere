# Skills Directory Optimization

**Problem**: Skills were duplicated in `.agent/`, `.agents/`, and `.claude/` (198 markdown files × 3 = ~600 files loaded into context).

**Solution**: Use `.agents/skills/` as the canonical source. Other agents should reference it via:

## For Claude Code

`.claude/skills/` should be a **symlink** to `../.agents/skills`:

```bash
# Windows (requires Developer Mode or admin)
cmd /c mklink /D .claude\skills .agents\skills

# Git Bash / WSL
ln -s ../.agents/skills .claude/skills
```

**Fallback** (if symlinks fail): Use the sync script.

## For Other Agents

Add to their configuration:

```json
{
  "skillsPath": ".agents/skills"
}
```

Or configure their IDE to point at `.agents/skills/` directly.

## Sync Script (Windows fallback)

If symlinks don't work on Windows:

**scripts/sync-skills.ps1**:
```powershell
# Copy .agents/skills to .claude/skills (only when modified)
robocopy .agents\skills .claude\skills /MIR /XO
```

Run in bootstrap.sh:
```bash
powershell -File scripts/sync-skills.ps1
```

## Current State

- **Canonical source**: `.agents/skills/` (66 skill markdown files)
- **Duplicates removed**: `.agent/` (deleted), `.claude/skills/` (to be symlinked or synced)

This reduces skill context from ~200 files to ~66 files.
