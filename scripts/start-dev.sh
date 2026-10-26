#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

echo "============================================="
echo "  Starting ShopSphere Development Environment"
echo "============================================="

echo ""
echo "[1/5] Starting Docker infrastructure (SQL Server, RabbitMQ, Redis)..."
docker compose -f deploy/compose/docker-compose.yml up -d
docker compose -f deploy/compose/docker-compose.yml ps

sleep 3

PIDS=()

cleanup() {
    echo ""
    echo "Stopping all launched services..."
    for pid in "${PIDS[@]}"; do
        if kill -0 "$pid" 2>/dev/null; then
            echo "  Stopping PID $pid..."
            kill "$pid" 2>/dev/null || true
        fi
    done
    wait 2>/dev/null || true
    echo "All services stopped."
}

trap cleanup EXIT INT TERM

echo ""
echo "[2/5] Building solution once..."
dotnet build ShopSphere.sln --configuration Debug --nologo -v q

echo ""
echo "[3/5] Starting backend services & workers (--no-build)..."

export ASPNETCORE_ENVIRONMENT="Development"

start_dotnet() {
    local name="$1"
    local path="$2"
    local port="${3:-}"

    if [[ -n "$port" ]]; then
        echo "  Launching $name (Port $port)..."
        dotnet run --no-build --project "$path" --urls "http://localhost:$port" &
    else
        echo "  Launching $name..."
        dotnet run --no-build --project "$path" &
    fi
    PIDS+=($!)
    sleep 1
}

start_dotnet "Catalog.Api"         "src/Services/Catalog/Catalog.Api"               "5001"
start_dotnet "Catalog.Worker"      "src/Services/Catalog/Catalog.Worker"
start_dotnet "Cart.Api"            "src/Services/Cart/Cart.Api"                     "5002"
start_dotnet "Inventory.Api"       "src/Services/Inventory/Inventory.Api"           "5003"
start_dotnet "Inventory.Worker"    "src/Services/Inventory/Inventory.Worker"
start_dotnet "Payment.Api"         "src/Services/Payment/Payment.Api"               "5004"
start_dotnet "Payment.Worker"      "src/Services/Payment/Payment.Worker"
start_dotnet "Order.Api"           "src/Services/Order/Order.Api"                   "5005"
start_dotnet "Order.Worker"        "src/Services/Order/Order.Worker"
start_dotnet "Shipping.Api"        "src/Services/Shipping/Shipping.Api"             "5006"
start_dotnet "Shipping.Worker"     "src/Services/Shipping/Shipping.Worker"
start_dotnet "Identity.Api"        "src/Services/Identity/Identity.Api"             "5007"
start_dotnet "Notification.Api"    "src/Services/Notification/Notification.Api"     "5008"
start_dotnet "Notification.Worker" "src/Services/Notification/Notification.Worker"
start_dotnet "YARP Gateway"        "src/Gateway/ShopSphere.Gateway"                 "5000"

echo ""
echo "[4/5] Starting Vite Frontend SPA (Port 3000)..."
pnpm --dir src/Web run dev &
PIDS+=($!)

echo ""
echo "[5/5] Environment Ready!"
echo "--------------------------------------------------------"
echo "  Frontend:         http://localhost:3000"
echo "  Gateway:          http://localhost:5000"
echo "  Catalog API:      http://localhost:5001"
echo "  Cart API:         http://localhost:5002"
echo "  Inventory API:    http://localhost:5003"
echo "  Payment API:      http://localhost:5004"
echo "  Order API:        http://localhost:5005"
echo "  Shipping API:     http://localhost:5006"
echo "  Identity API:     http://localhost:5007"
echo "  Notification API: http://localhost:5008"
echo "  RabbitMQ UI:      http://localhost:15672 (guest/guest)"
echo "--------------------------------------------------------"
echo "Press Ctrl+C to terminate all services."

wait
