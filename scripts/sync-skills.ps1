# Sync vendored skills to Claude Code agent directories
# Use this if symlinks don't work on Windows

$ErrorActionPreference = "Stop"

$source = Join-Path $PSScriptRoot ".." ".agents" "skills"
$claTarget = Join-Path $PSScriptRoot ".." ".claude" "skills"
$agentTarget = Join-Path $PSScriptRoot ".." ".agent" "skills"

if (-not (Test-Path $source)) {
    Write-Error "Source directory not found: $source"
    exit 1
}

# Create parent directories if missing
$claParent = Split-Path $claTarget -Parent
$agentParent = Split-Path $agentTarget -Parent
if (-not (Test-Path $claParent)) { New-Item -ItemType Directory -Path $claParent -Force | Out-Null }
if (-not (Test-Path $agentParent)) { New-Item -ItemType Directory -Path $agentParent -Force | Out-Null }

# Use robocopy for efficient mirroring (only changed files)
$null = robocopy $source $claTarget /MIR /XO /NJH /NJS /NFL /NDL
if ($LASTEXITCODE -gt 7) {
    Write-Error "robocopy failed for .claude/skills (exit code $LASTEXITCODE)"
    exit $LASTEXITCODE
}

$null = robocopy $source $agentTarget /MIR /XO /NJH /NJS /NFL /NDL
if ($LASTEXITCODE -gt 7) {
    Write-Error "robocopy failed for .agent/skills (exit code $LASTEXITCODE)"
    exit $LASTEXITCODE
}

Write-Output "Skills synced from $source"
Write-Output "  -> $claTarget"
Write-Output "  -> $agentTarget"
