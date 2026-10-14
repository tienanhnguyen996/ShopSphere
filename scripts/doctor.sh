#!/usr/bin/env bash

# ShopSphere Environment Health Check
# Exit 1 on any failure, print fix instructions

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

FAILED=0

echo "🏥 ShopSphere Doctor"
echo "===================="
echo ""

# 1. Tool versions
echo "📋 Checking tool versions..."

check_version() {
  local tool=$1
  local expected=$2
  local cmd=$3

  if command -v $tool &> /dev/null; then
    local version=$(eval $cmd 2>&1 | grep -oE '[0-9]+\.[0-9]+\.[0-9]+' | head -1 || echo "unknown")
    if [[ "$version" =~ ^$expected ]]; then
      echo -e "${GREEN}✅${NC} $tool $version"
    else
      echo -e "${YELLOW}⚠️${NC}  $tool $version (expected $expected.x)"
    fi
  else
    echo -e "${RED}❌${NC} $tool not found"
    echo "   Fix: Install $tool or check PATH"
    FAILED=1
  fi
}

check_version "dotnet" "8\.0" "dotnet --version"
check_version "node" "20\." "node --version"
check_version "pnpm" "[0-9]+" "pnpm --version"
check_version "terraform" "1\." "terraform --version"
check_version "docker" "[0-9]+" "docker --version"

echo ""

# 2. Docker running
echo "🐳 Checking Docker..."
if docker ps &> /dev/null; then
  echo -e "${GREEN}✅${NC} Docker is running"
else
  echo -e "${RED}❌${NC} Docker is not running or not accessible"
  echo "   Fix: Start Docker Desktop or check docker daemon"
  FAILED=1
fi

echo ""

# 3. .env exists with required keys
echo "🔑 Checking .env file..."
if [ -f ".env" ]; then
  echo -e "${GREEN}✅${NC} .env exists"

  # Check for required keys
  if grep -q "GITHUB_TOKEN=" .env && ! grep -q "GITHUB_TOKEN=ghp_YOUR" .env; then
    echo -e "${GREEN}  ✓${NC} GITHUB_TOKEN set"
  else
    echo -e "${YELLOW}  ⚠️${NC}  GITHUB_TOKEN not set or still placeholder"
    echo "     Fix: Get token from https://github.com/settings/tokens/new"
  fi

  if grep -q "NOTION_TOKEN=" .env && ! grep -q "NOTION_TOKEN=secret_YOUR" .env; then
    echo -e "${GREEN}  ✓${NC} NOTION_TOKEN set"
  else
    echo -e "${YELLOW}  ⚠️${NC}  NOTION_TOKEN not set or still placeholder"
    echo "     Fix: Get token from https://www.notion.so/my-integrations"
  fi
else
  echo -e "${RED}❌${NC} .env file missing"
  echo "   Fix: cp .env.example .env && edit .env"
  FAILED=1
fi

echo ""

# 4. Compose infra healthy
echo "🏗️  Checking Docker Compose infrastructure..."
COMPOSE_FILE="deploy/compose/docker-compose.yml"

if [ -n "$CI" ]; then
  echo -e "${YELLOW}⚠️${NC}  Skipping Docker Compose running services check in CI"
elif [ -f "$COMPOSE_FILE" ]; then
  if docker compose -f "$COMPOSE_FILE" ps --format json &> /dev/null; then
    # Check if services are running
    RUNNING=$(docker compose -f "$COMPOSE_FILE" ps --format json 2>/dev/null | jq -r '.[] | select(.State == "running") | .Name' | wc -l || echo "0")
    TOTAL=$(docker compose -f "$COMPOSE_FILE" config --services | wc -l || echo "0")

    if [ "$RUNNING" -eq "$TOTAL" ] && [ "$TOTAL" -gt 0 ]; then
      echo -e "${GREEN}✅${NC} All compose services running ($RUNNING/$TOTAL)"

      # Check health status
      HEALTHY=$(docker compose -f "$COMPOSE_FILE" ps --format json 2>/dev/null | jq -r '.[] | select(.Health == "healthy") | .Name' | wc -l || echo "0")
      if [ "$HEALTHY" -eq "$TOTAL" ]; then
        echo -e "${GREEN}  ✓${NC} All services healthy"
      else
        echo -e "${YELLOW}  ⚠️${NC}  Only $HEALTHY/$TOTAL services healthy (may still be starting)"
        echo "     Wait ~30s and run doctor again, or check logs:"
        echo "     docker compose -f $COMPOSE_FILE logs"
      fi
    elif [ "$TOTAL" -eq 0 ]; then
      echo -e "${YELLOW}⚠️${NC}  No services defined in compose file"
    else
      echo -e "${RED}❌${NC} Only $RUNNING/$TOTAL services running"
      echo "   Fix: docker compose -f $COMPOSE_FILE up -d"
      FAILED=1
    fi
  else
    echo -e "${YELLOW}⚠️${NC}  Compose services not started"
    echo "   Fix: docker compose -f $COMPOSE_FILE up -d"
  fi
else
  echo -e "${YELLOW}⚠️${NC}  docker-compose.yml not found (foundation only)"
fi

echo ""

# 5. MCP config valid
echo "🔌 Checking MCP configuration..."
if [ -f ".mcp.json" ]; then
  if jq empty .mcp.json &> /dev/null; then
    echo -e "${GREEN}✅${NC} .mcp.json is valid JSON"

    # Check for inline secrets (security issue)
    if grep -qE '(ghp_|secret_|key_|password|token)' .mcp.json | grep -v '\${'; then
      echo -e "${RED}  ❌${NC} Possible secrets found in .mcp.json"
      echo "     Fix: Use \${ENV_VAR} references, never commit secrets"
      FAILED=1
    else
      echo -e "${GREEN}  ✓${NC} No inline secrets detected"
    fi
  else
    echo -e "${RED}❌${NC} .mcp.json is invalid JSON"
    echo "   Fix: Run 'jq . .mcp.json' to see parse errors"
    FAILED=1
  fi
else
  echo -e "${YELLOW}⚠️${NC}  .mcp.json not found"
fi

echo ""

# 6. Skills present
echo "🛠️  Checking vendored skills..."
if [ -d ".agents/skills" ]; then
  if [ -f ".agents/skills/SOURCES.md" ]; then
    echo -e "${GREEN}✅${NC} Skills vendored with provenance"
  else
    echo -e "${YELLOW}⚠️${NC}  .agents/skills exists but missing SOURCES.md"
    echo "   Fix: Document skill provenance in .agents/skills/SOURCES.md"
  fi

  # Check symlinks
  if [ -L ".claude/skills" ] || [ -d ".claude/skills" ]; then
    echo -e "${GREEN}  ✓${NC} .claude/skills accessible"
  else
    echo -e "${YELLOW}  ⚠️${NC}  .claude/skills not found"
    echo "     Fix: Re-run bash scripts/bootstrap.sh"
  fi

  if [ -L ".agent/skills" ] || [ -d ".agent/skills" ]; then
    echo -e "${GREEN}  ✓${NC} .agent/skills accessible"
  else
    echo -e "${YELLOW}  ⚠️${NC}  .agent/skills not found"
    echo "     Fix: Re-run bash scripts/bootstrap.sh"
  fi
else
  echo -e "${RED}❌${NC} .agents/skills directory not found"
  echo "   Fix: Vendor skills from https://github.com/coleam00/ai-agent-superpowers"
  FAILED=1
fi

echo ""

# 7. Lint and Code Style tooling
echo "🎨 Checking linting and formatting tooling..."
if [ -f "scripts/lint.sh" ] && [ -f "scripts/fix-lint.sh" ]; then
  echo -e "${GREEN}✅${NC} Lint scripts present (scripts/lint.sh, scripts/fix-lint.sh)"
else
  echo -e "${RED}❌${NC} Lint scripts missing"
  echo "   Fix: Ensure scripts/lint.sh and scripts/fix-lint.sh exist"
  FAILED=1
fi

echo ""
echo "===================="

if [ $FAILED -eq 0 ]; then
  echo -e "${GREEN}🎉 Environment ready!${NC}"
  echo ""
  echo "Next: docker compose -f deploy/compose/docker-compose.yml up -d"
  exit 0
else
  echo -e "${RED}❌ Some checks failed. Fix issues above before proceeding.${NC}"
  exit 1
fi
