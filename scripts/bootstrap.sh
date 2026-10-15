#!/usr/bin/env bash
set -e

echo "🚀 ShopSphere Bootstrap"
echo "======================"

echo ""
echo "🔧 Enabling corepack for pnpm..."
corepack enable || echo "⚠️  Corepack enable failed (might already be enabled)"

echo ""
echo "📦 Restoring .NET workload..."
if [ -f "*.sln" ] || [ -f "*.csproj" ]; then
  dotnet restore || echo "⚠️  No .NET projects to restore yet"
else
  echo "⏭️  No .NET projects found yet (foundation only)"
fi

echo ""
echo "📦 Installing pnpm dependencies..."
if [ -f "package.json" ]; then
  pnpm install
else
  echo "⏭️  No package.json found yet (frontend not implemented)"
fi

echo ""
echo "🔗 Creating skill symlinks..."
mkdir -p .claude .agent

if [ ! -L ".claude/skills" ]; then
  if ln -s ../.agents/skills .claude/skills 2>/dev/null; then
    echo "✅ .claude/skills symlink created"
  else
    echo "⚠️  Symlink failed, using sync script fallback"
    if command -v pwsh &> /dev/null; then
      pwsh scripts/sync-skills.ps1 2>/dev/null || echo "⚠️  sync-skills.ps1 not found yet"
    else
      echo "⚠️  PowerShell not found, symlink fallback unavailable"
    fi
  fi
else
  echo "✅ .claude/skills already exists"
fi

if [ ! -L ".agent/skills" ]; then
  if ln -s ../.agents/skills .agent/skills 2>/dev/null; then
    echo "✅ .agent/skills symlink created"
  else
    echo "⚠️  Symlink failed (see above)"
  fi
else
  echo "✅ .agent/skills already exists"
fi

echo ""
echo "🎯 Installing git hooks..."
if [ -d ".git" ]; then
  cp scripts/verify-commit-msg.sh .git/hooks/commit-msg 2>/dev/null || true
  chmod +x .git/hooks/commit-msg 2>/dev/null || true
  echo "✅ commit-msg hook installed (enforces [Task-XX] in commits)"

  cp scripts/verify-pre-commit.sh .git/hooks/pre-commit 2>/dev/null || true
  chmod +x .git/hooks/pre-commit 2>/dev/null || true
  echo "✅ pre-commit hook installed (enforces lint & formatting checks)"
else
  echo "⏭️  Not a git repository yet"
fi

echo ""
echo "✅ Bootstrap complete!"
echo ""
echo "Next steps:"
echo "  1. Copy .env.example to .env and fill in tokens"
echo "  2. Run: bash scripts/doctor.sh"
echo "  3. Start infrastructure: docker compose -f deploy/compose/docker-compose.yml up -d"
