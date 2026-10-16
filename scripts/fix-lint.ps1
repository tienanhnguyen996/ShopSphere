# ShopSphere Lint and Format Fixer (PowerShell)
$ErrorActionPreference = "Continue"

Write-Host "ShopSphere Lint and Format Fixer" -ForegroundColor Cyan
Write-Host "================================"

if (Test-Path "$env:USERPROFILE\.dotnet") {
    $env:PATH = "$env:USERPROFILE\.dotnet;$env:PATH"
}

# 1. .NET Format Fix
Write-Host "`nFixing .NET formatting..." -ForegroundColor Cyan
$dotnetVersion = dotnet --version 2>$null
if ($dotnetVersion -and $dotnetVersion.StartsWith("8.0")) {
    dotnet format
    Write-Host "[PASS] .NET format applied" -ForegroundColor Green
} elseif (Get-Command docker -ErrorAction SilentlyContinue) {
    Write-Host "Using Docker container for .NET 8 SDK..."
    docker run --rm -v "${PWD}:/workspace" -w /workspace mcr.microsoft.com/dotnet/sdk:8.0 dotnet format
    Write-Host "[PASS] .NET format applied via Docker" -ForegroundColor Green
} else {
    Write-Host "[WARN] Skipping .NET format (neither .NET 8 SDK nor Docker found)" -ForegroundColor Yellow
}

# 2. Frontend Web Fix
Write-Host "`nFixing Frontend linting and formatting..." -ForegroundColor Cyan
if (Test-Path "src/Web") {
    pnpm --dir src/Web lint:fix
    pnpm --dir src/Web format
    Write-Host "[PASS] Frontend format and lint fixes applied" -ForegroundColor Green
}

Write-Host "`nFinished running lint fixers!" -ForegroundColor Green
