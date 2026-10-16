# ShopSphere Environment Health Check (PowerShell)
$ErrorActionPreference = "Continue"

Write-Host "ShopSphere Doctor" -ForegroundColor Cyan
Write-Host "================="
Write-Host ""

$failed = $false

if (Test-Path "$env:USERPROFILE\.dotnet") {
    $env:PATH = "$env:USERPROFILE\.dotnet;$env:PATH"
}
$machinePath = [System.Environment]::GetEnvironmentVariable("Path", "Machine")
$userPath = [System.Environment]::GetEnvironmentVariable("Path", "User")
$env:PATH = "$env:PATH;$machinePath;$userPath"

# 1. Tool versions
Write-Host "Checking tool versions..." -ForegroundColor Cyan

function Check-ToolVersion {
    param(
        [string]$Tool,
        [string]$ExpectedPattern,
        [scriptblock]$GetVersion
    )

    if (Get-Command $Tool -ErrorAction SilentlyContinue) {
        try {
            $ver = & $GetVersion
            if ($ver -match $ExpectedPattern) {
                Write-Host "  [PASS] $Tool $ver" -ForegroundColor Green
            } else {
                Write-Host "  [WARN] $Tool $ver (expected $ExpectedPattern)" -ForegroundColor Yellow
            }
        } catch {
            Write-Host "  [WARN] $Tool found, but error getting version" -ForegroundColor Yellow
        }
    } else {
        Write-Host "  [FAIL] $Tool not found" -ForegroundColor Red
        Write-Host "         Fix: Install $Tool or add to PATH"
        $script:failed = $true
    }
}

Check-ToolVersion "dotnet" "^10\." { (dotnet --version 2>&1).Trim() }
Check-ToolVersion "node" "^v?2[02]\." { (node --version 2>&1).Trim() }
Check-ToolVersion "pnpm" "^\d+" { (pnpm --version 2>&1).Trim() }
Check-ToolVersion "terraform" "Terraform v1\." { (terraform --version 2>&1 | Select-Object -First 1).Trim() }
Check-ToolVersion "docker" "Docker version \d+" { (docker --version 2>&1 | Select-Object -First 1).Trim() }

Write-Host ""

# 2. Docker running
Write-Host "Checking Docker..." -ForegroundColor Cyan
$dockerRunning = $false
if (Get-Command docker -ErrorAction SilentlyContinue) {
    docker ps 2>&1 | Out-Null
    if ($LASTEXITCODE -eq 0) {
        $dockerRunning = $true
        Write-Host "  [PASS] Docker is running" -ForegroundColor Green
    } else {
        Write-Host "  [FAIL] Docker is not running or not accessible" -ForegroundColor Red
        Write-Host "         Fix: Start Docker Desktop or check daemon"
        $failed = $true
    }
} else {
    Write-Host "  [FAIL] Docker command not found" -ForegroundColor Red
    $failed = $true
}

Write-Host ""

# 3. .env file
Write-Host "Checking .env file..." -ForegroundColor Cyan
if (Test-Path ".env") {
    Write-Host "  [PASS] .env exists" -ForegroundColor Green
    $envContent = Get-Content ".env" -Raw
    if ($envContent -match "GITHUB_TOKEN=" -and $envContent -notmatch "GITHUB_TOKEN=ghp_YOUR") {
        Write-Host "    [OK] GITHUB_TOKEN set" -ForegroundColor Green
    } else {
        Write-Host "    [WARN] GITHUB_TOKEN not set or placeholder" -ForegroundColor Yellow
    }

    if ($envContent -match "NOTION_TOKEN=" -and $envContent -notmatch "NOTION_TOKEN=secret_YOUR") {
        Write-Host "    [OK] NOTION_TOKEN set" -ForegroundColor Green
    } else {
        Write-Host "    [WARN] NOTION_TOKEN not set or placeholder" -ForegroundColor Yellow
    }
} else {
    Write-Host "  [FAIL] .env file missing" -ForegroundColor Red
    Write-Host "         Fix: Copy .env.example to .env and configure"
    $failed = $true
}

Write-Host ""

# 4. MCP config
Write-Host "Checking MCP configuration..." -ForegroundColor Cyan
if (Test-Path ".mcp.json") {
    try {
        $json = Get-Content ".mcp.json" -Raw | ConvertFrom-Json
        Write-Host "  [PASS] .mcp.json is valid JSON" -ForegroundColor Green

        $rawJson = Get-Content ".mcp.json" -Raw
        if ($rawJson -match 'ghp_[A-Za-z0-9_]{30,}' -or $rawJson -match 'secret_[A-Za-z0-9_]{30,}') {
            Write-Host "  [FAIL] Hardcoded secrets found in .mcp.json" -ForegroundColor Red
            $failed = $true
        } else {
            Write-Host "    [OK] No inline secrets detected" -ForegroundColor Green
        }
    } catch {
        Write-Host "  [FAIL] .mcp.json is invalid JSON" -ForegroundColor Red
        $failed = $true
    }
} else {
    Write-Host "  [WARN] .mcp.json not found" -ForegroundColor Yellow
}

Write-Host ""

# 5. Skills present
Write-Host "Checking skills..." -ForegroundColor Cyan
if ((Test-Path ".agent/skills") -or (Test-Path ".agents/skills")) {
    Write-Host "  [PASS] Skills directory accessible" -ForegroundColor Green
} else {
    Write-Host "  [FAIL] Skills directory not found" -ForegroundColor Red
    $failed = $true
}

Write-Host ""

# 6. Lint scripts
Write-Host "Checking lint scripts..." -ForegroundColor Cyan
if (((Test-Path "scripts/lint.sh") -or (Test-Path "scripts/lint.ps1")) -and ((Test-Path "scripts/fix-lint.sh") -or (Test-Path "scripts/fix-lint.ps1"))) {
    Write-Host "  [PASS] Lint scripts present" -ForegroundColor Green
} else {
    Write-Host "  [FAIL] Lint scripts missing" -ForegroundColor Red
    $failed = $true
}

Write-Host ""
Write-Host "================="
if ($failed) {
    Write-Host "Some checks failed. Fix issues above." -ForegroundColor Red
    exit 1
} else {
    Write-Host "Environment ready!" -ForegroundColor Green
    exit 0
}
