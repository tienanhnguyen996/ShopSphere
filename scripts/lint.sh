#!/usr/bin/env bash

# ShopSphere Lint & Format Verification
# Verifies .NET and Frontend code style without modifying files.

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

FAILED=0

echo "🔍 ShopSphere Lint & Format Check"
echo "=================================="

# 1. .NET Format Check
echo ""
echo "📦 Checking .NET formatting..."
if dotnet --version 2>/dev/null | grep -q "^8\.0"; then
  if dotnet format --verify-no-changes; then
    echo -e "${GREEN}✅${NC} .NET format check passed"
  else
    echo -e "${RED}❌${NC} .NET format check failed. Run 'bash scripts/fix-lint.sh' to fix."
    FAILED=1
  fi
elif command -v docker &>/dev/null && docker ps &>/dev/null; then
  echo "Using Docker container for .NET 8 SDK..."
  if docker run --rm -v "$(pwd):/workspace" -w /workspace mcr.microsoft.com/dotnet/sdk:8.0 dotnet format --verify-no-changes; then
    echo -e "${GREEN}✅${NC} .NET format check passed"
  else
    echo -e "${RED}❌${NC} .NET format check failed. Run 'bash scripts/fix-lint.sh' to fix."
    FAILED=1
  fi
else
  echo -e "${YELLOW}⚠️${NC}  Skipping .NET format check (neither .NET 8 SDK nor Docker found)"
fi

# 2. Frontend Web Check
echo ""
echo "🌐 Checking Frontend (ESLint & Prettier)..."
if [ -d "src/Web" ]; then
  if pnpm --dir src/Web lint; then
    echo -e "${GREEN}✅${NC} Frontend ESLint check passed"
  else
    echo -e "${RED}❌${NC} Frontend ESLint check failed. Run 'bash scripts/fix-lint.sh' to fix."
    FAILED=1
  fi

  if pnpm --dir src/Web format:check; then
    echo -e "${GREEN}✅${NC} Frontend Prettier format check passed"
  else
    echo -e "${RED}❌${NC} Frontend Prettier format check failed. Run 'bash scripts/fix-lint.sh' to fix."
    FAILED=1
  fi
fi

echo ""
if [ $FAILED -eq 0 ]; then
  echo -e "${GREEN}🎉 All lint and format checks passed!${NC}"
  exit 0
else
  echo -e "${RED}💥 Lint checks failed. Please fix issues before committing.${NC}"
  exit 1
fi
