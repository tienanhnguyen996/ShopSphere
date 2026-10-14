#!/usr/bin/env bash

# ShopSphere Git Pre-Commit Hook
# Enforces code formatting and lint verification before commits.

set -e

echo "🔍 Running pre-commit lint verification..."

if [ -f "scripts/lint.sh" ]; then
  bash scripts/lint.sh
elif [ -f "scripts/lint.ps1" ] && command -v powershell &>/dev/null; then
  powershell -ExecutionPolicy Bypass -File scripts/lint.ps1
fi
