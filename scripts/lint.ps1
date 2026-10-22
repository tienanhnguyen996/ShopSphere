# ShopSphere Lint and Format Verification (PowerShell)
$ErrorActionPreference = "Continue"

Write-Host "ShopSphere Lint and Format Check" -ForegroundColor Cyan
Write-Host "================================"

$failed = $false

if (Test-Path "$env:USERPROFILE\.dotnet") {
    $env:PATH = "$env:USERPROFILE\.dotnet;$env:PATH"
}

# 1. .NET Format Check
Write-Host "`nChecking .NET formatting..." -ForegroundColor Cyan
$dotnetVersion = dotnet --version 2>$null
$dockerRunning = $false
if (Get-Command docker -ErrorAction SilentlyContinue) {
    docker ps 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) { $dockerRunning = $true }
}

if ($dotnetVersion -and $dotnetVersion.StartsWith("10.0")) {
    dotnet format --verify-no-changes
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[FAIL] .NET format check failed. Run './scripts/fix-lint.ps1' to fix." -ForegroundColor Red
        $failed = $true
    } else {
        Write-Host "[PASS] .NET format check passed" -ForegroundColor Green
    }
} elseif ($dockerRunning) {
    Write-Host "Using Docker container for .NET 10 SDK..."
    docker run --rm -v "${PWD}:/workspace" -w /workspace mcr.microsoft.com/dotnet/sdk:10.0 dotnet format --verify-no-changes
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[FAIL] .NET format check failed. Run './scripts/fix-lint.ps1' to fix." -ForegroundColor Red
        $failed = $true
    } else {
        Write-Host "[PASS] .NET format check passed" -ForegroundColor Green
    }
} else {
    Write-Host "[WARN] Skipping .NET format check (neither .NET 10 SDK nor Docker found)" -ForegroundColor Yellow
}

# 2. Frontend Check
Write-Host "`nChecking Frontend (ESLint and Prettier)..." -ForegroundColor Cyan
if (Test-Path "src/Web") {
    pnpm --dir src/Web lint
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[FAIL] Frontend ESLint check failed. Run './scripts/fix-lint.ps1' to fix." -ForegroundColor Red
        $failed = $true
    } else {
        Write-Host "[PASS] Frontend ESLint check passed" -ForegroundColor Green
    }

    pnpm --dir src/Web format:check
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[FAIL] Frontend Prettier format check failed. Run './scripts/fix-lint.ps1' to fix." -ForegroundColor Red
        $failed = $true
    } else {
        Write-Host "[PASS] Frontend Prettier format check passed" -ForegroundColor Green
    }
}

if ($failed) {
    Write-Host "`nLint checks failed. Please fix issues before committing." -ForegroundColor Red
    exit 1
} else {
    Write-Host "`nAll lint and format checks passed!" -ForegroundColor Green
    exit 0
}
