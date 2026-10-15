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

# Remove existing targets
if (Test-Path $claTarget) { Remove-Item -Recurse -Force $claTarget }
if (Test-Path $agentTarget) { Remove-Item -Recurse -Force $agentTarget }

# Copy to both targets
Copy-Item -Recurse $source $claTarget
Copy-Item -Recurse $source $agentTarget

Write-Output "Skills synced from $source"
Write-Output "  -> $claTarget"
Write-Output "  -> $agentTarget"
