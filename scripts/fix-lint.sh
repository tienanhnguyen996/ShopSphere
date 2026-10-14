#!/usr/bin/env bash

# ShopSphere Lint & Format Fixer
# Automatically fixes .NET formatting and Frontend ESLint/Prettier issues.

set -e

GREEN='\033[0;32m'
NC='\033[0m'

echo "🔧 ShopSphere Lint & Format Fixer"
echo "=================================="

# 1. .NET Format Fix
echo ""
echo "📦 Fixing .NET formatting..."
if dotnet --version 2>/dev/null | grep -q "^8\.0"; then
  dotnet format
  echo -e "${GREEN}✅${NC} .NET format applied"
elif command -v docker &>/dev/null; then
  echo "Using Docker container for .NET 8 SDK..."
  docker run --rm -v "$(pwd):/workspace" -w /workspace mcr.microsoft.com/dotnet/sdk:8.0 dotnet format
  echo -e "${GREEN}✅${NC} .NET format applied via Docker"
else
  echo "⚠️ Skipping .NET format (neither .NET 8 SDK nor Docker found)"
fi

# 2. Frontend Web Fix
echo ""
echo "🌐 Fixing Frontend linting & formatting..."
if [ -d "src/Web" ]; then
  pnpm --dir src/Web lint:fix || true
  pnpm --dir src/Web format || true
  echo -e "${GREEN}✅${NC} Frontend format & lint fixes applied"
fi

echo ""
echo -e "${GREEN}🎉 Finished running lint fixers!${NC}"
