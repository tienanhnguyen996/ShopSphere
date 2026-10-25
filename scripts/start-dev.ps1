<#
.SYNOPSIS
    Starts all ShopSphere infrastructure, backend microservices, workers, API Gateway, and frontend.
.DESCRIPTION
    1. Ensures Docker infrastructure (SQL Server, RabbitMQ, Redis) is up.
    2. Builds ShopSphere.sln once upfront.
    3. Starts each .NET microservice API and Worker (--no-build) in the background.
    4. Starts the YARP API Gateway and Vite React Frontend.
    5. Tracks all process IDs and cleanly shuts them down on Ctrl+C or script exit.
.PARAMETER SeparateWindows
    If specified, launches each service in its own terminal window instead of background processes.
.EXAMPLE
    pwsh scripts/start-dev.ps1
    pwsh scripts/start-dev.ps1 -SeparateWindows
#>

[CmdletBinding()]
param(
    [switch]$SeparateWindows
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  Starting ShopSphere Development Environment" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

# 1. Start Docker Infrastructure
Write-Host "`n[1/5] Starting Docker infrastructure (SQL Server, RabbitMQ, Redis)..." -ForegroundColor Yellow
docker compose -f deploy/compose/docker-compose.yml up -d
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to start Docker Compose infrastructure. Ensure Docker Desktop is running."
}

Write-Host "Checking infrastructure status..." -ForegroundColor Gray
docker compose -f deploy/compose/docker-compose.yml ps

# Give SQL Server and RabbitMQ a brief moment to ensure ports are listening
Start-Sleep -Seconds 3

# 2. Define all services to launch
$services = @(
    @{ Name = "Catalog.Api";         Path = "src/Services/Catalog/Catalog.Api";               Port = 5001; Type = "API" },
    @{ Name = "Catalog.Worker";      Path = "src/Services/Catalog/Catalog.Worker";            Port = $null; Type = "Worker" },
    @{ Name = "Cart.Api";            Path = "src/Services/Cart/Cart.Api";                     Port = 5002; Type = "API" },
    @{ Name = "Inventory.Api";       Path = "src/Services/Inventory/Inventory.Api";           Port = 5003; Type = "API" },
    @{ Name = "Inventory.Worker";    Path = "src/Services/Inventory/Inventory.Worker";        Port = $null; Type = "Worker" },
    @{ Name = "Payment.Api";         Path = "src/Services/Payment/Payment.Api";               Port = 5004; Type = "API" },
    @{ Name = "Payment.Worker";      Path = "src/Services/Payment/Payment.Worker";            Port = $null; Type = "Worker" },
    @{ Name = "Order.Api";           Path = "src/Services/Order/Order.Api";                   Port = 5005; Type = "API" },
    @{ Name = "Order.Worker";        Path = "src/Services/Order/Order.Worker";                Port = $null; Type = "Worker" },
    @{ Name = "Shipping.Api";        Path = "src/Services/Shipping/Shipping.Api";             Port = 5006; Type = "API" },
    @{ Name = "Shipping.Worker";     Path = "src/Services/Shipping/Shipping.Worker";          Port = $null; Type = "Worker" },
    @{ Name = "Identity.Api";        Path = "src/Services/Identity/Identity.Api";             Port = 5007; Type = "API" },
    @{ Name = "Notification.Api";    Path = "src/Services/Notification/Notification.Api";     Port = 5008; Type = "API" },
    @{ Name = "Notification.Worker"; Path = "src/Services/Notification/Notification.Worker";  Port = $null; Type = "Worker" },
    @{ Name = "YARP Gateway";        Path = "src/Gateway/ShopSphere.Gateway";                 Port = 5000; Type = "Gateway" }
)

$startedProcesses = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()

function Stop-AllServices {
    Write-Host "`nStopping all launched services..." -ForegroundColor Yellow
    foreach ($proc in $startedProcesses) {
        if (-not $proc.HasExited) {
            Write-Host "  Stopping PID $($proc.Id)..." -ForegroundColor Gray
            try {
                if ($IsWindows -or $env:OS -like "*Windows*") {
                    & taskkill /F /T /PID $proc.Id 2>$null | Out-Null
                } else {
                    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
                }
            } catch {
                # ignore already terminated processes
            }
        }
    }
    Get-Process | Where-Object { $_.ProcessName -match '^(Catalog|Cart|Inventory|Payment|Order|Shipping|Identity|Notification)\.(Api|Worker)$|^ShopSphere\.Gateway$' } | Stop-Process -Force -ErrorAction SilentlyContinue
    Write-Host "All services stopped." -ForegroundColor Green
}

# Clean up any leftover service processes before building so DLLs are not locked
Get-Process | Where-Object { $_.ProcessName -match '^(Catalog|Cart|Inventory|Payment|Order|Shipping|Identity|Notification)\.(Api|Worker)$|^ShopSphere\.Gateway$' } | Stop-Process -Force -ErrorAction SilentlyContinue

# 3. Build Solution Once
Write-Host "`n[2/5] Building solution once..." -ForegroundColor Yellow
dotnet build ShopSphere.sln --configuration Debug --nologo -v q
if ($LASTEXITCODE -ne 0) {
    Write-Error "Solution build failed."
}

# 4. Launch Backend Services
Write-Host "`n[3/5] Starting backend services & workers (--no-build)..." -ForegroundColor Yellow

$env:ASPNETCORE_ENVIRONMENT = "Development"

foreach ($s in $services) {
    $portInfo = if ($s.Port) { " (Port $($s.Port))" } else { "" }
    Write-Host "  Launching $($s.Name)$portInfo..." -ForegroundColor Cyan

    $args = if ($s.Port) { "run --no-build --project $($s.Path) --urls http://localhost:$($s.Port)" } else { "run --no-build --project $($s.Path)" }

    if ($SeparateWindows) {
        $p = Start-Process powershell -ArgumentList "-NoExit", "-Command", "Write-Host 'Starting $($s.Name)...' -ForegroundColor Cyan; cd '$RepoRoot'; dotnet $args" -PassThru
    } else {
        $pInfo = New-Object System.Diagnostics.ProcessStartInfo
        $pInfo.FileName = "dotnet"
        $pInfo.Arguments = $args
        $pInfo.WorkingDirectory = $RepoRoot
        $pInfo.UseShellExecute = $false
        $pInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development"

        $p = [System.Diagnostics.Process]::Start($pInfo)
    }

    $startedProcesses.Add($p)
    Start-Sleep -Milliseconds 600
}

# 5. Launch Frontend SPA
Write-Host "`n[4/5] Starting Vite Frontend SPA (Port 3000)..." -ForegroundColor Yellow

if ($SeparateWindows) {
    $webProc = Start-Process powershell -ArgumentList "-NoExit", "-Command", "Write-Host 'Starting Vite Frontend...' -ForegroundColor Green; cd '$RepoRoot/src/Web'; pnpm run dev" -PassThru
} else {
    $pInfo = New-Object System.Diagnostics.ProcessStartInfo
    if ($IsWindows -or $env:OS -like "*Windows*") {
        $pInfo.FileName = "cmd.exe"
        $pInfo.Arguments = "/c pnpm --dir src/Web run dev"
    } else {
        $pInfo.FileName = "pnpm"
        $pInfo.Arguments = "--dir src/Web run dev"
    }
    $pInfo.WorkingDirectory = $RepoRoot
    $pInfo.UseShellExecute = $false

    $webProc = [System.Diagnostics.Process]::Start($pInfo)
}
$startedProcesses.Add($webProc)

# 6. Service Status Summary
Write-Host "`n[5/5] Environment Ready!" -ForegroundColor Green
Write-Host "--------------------------------------------------------" -ForegroundColor Gray
Write-Host "  Frontend:         http://localhost:3000" -ForegroundColor Green
Write-Host "  Gateway:          http://localhost:5000" -ForegroundColor White
Write-Host "  Catalog API:      http://localhost:5001" -ForegroundColor Gray
Write-Host "  Cart API:         http://localhost:5002" -ForegroundColor Gray
Write-Host "  Inventory API:    http://localhost:5003" -ForegroundColor Gray
Write-Host "  Payment API:      http://localhost:5004" -ForegroundColor Gray
Write-Host "  Order API:        http://localhost:5005" -ForegroundColor Gray
Write-Host "  Shipping API:     http://localhost:5006" -ForegroundColor Gray
Write-Host "  Identity API:     http://localhost:5007" -ForegroundColor Gray
Write-Host "  Notification API: http://localhost:5008" -ForegroundColor Gray
Write-Host "  RabbitMQ UI:      http://localhost:15672 (guest/guest)" -ForegroundColor Gray
Write-Host "--------------------------------------------------------" -ForegroundColor Gray
Write-Host "Press Ctrl+C to terminate all services." -ForegroundColor Yellow

# Wait and monitor processes
try {
    while ($true) {
        Start-Sleep -Seconds 2
        foreach ($proc in $startedProcesses) {
            if ($proc.HasExited -and $proc.ExitCode -ne 0) {
                Write-Host "[WARN] Process $($proc.Id) exited with code $($proc.ExitCode)" -ForegroundColor DarkYellow
            }
        }
    }
} finally {
    Stop-AllServices
}
