#!/usr/bin/env bash

# ShopSphere Commit Message Validator
# Enforces: <type>(<scope>): [Task-XX] <description>

COMMIT_MSG_FILE=$1
COMMIT_MSG=$(head -n 1 "$COMMIT_MSG_FILE")

# Allow merge commits
if [[ "$COMMIT_MSG" =~ ^Merge ]]; then
  exit 0
fi

# Allow initial / release commits
if [[ "$COMMIT_MSG" =~ ^(chore\(release\):|Initial commit) ]]; then
  exit 0
fi

PATTERN="^(feat|fix|infra|refactor|test|docs|chore)\([a-z0-9_-]+\): \[Task-[0-9]{2,}\] .+$"

if [[ ! "$COMMIT_MSG" =~ $PATTERN ]]; then
  echo ""
  echo "❌ INVALID COMMIT MESSAGE"
  echo "------------------------------------------------------------------"
  echo "Your commit message: '$COMMIT_MSG'"
  echo ""
  echo "Every commit must follow Conventional Commits and reference a Task ID:"
  echo "  <type>(<scope>): [Task-XX] <short description>"
  echo ""
  echo "Examples:"
  echo "  feat(catalog): [Task-12] implement product aggregate root"
  echo "  fix(payment): [Task-34] set state to PaymentUnknown on timeout"
  echo "  infra(buildingblocks): [Task-04] add ef core outbox interceptor"
  echo ""
  echo "Valid types: feat, fix, infra, refactor, test, docs, chore"
  echo "Valid scopes: buildingblocks, catalog, cart, order, inventory, payment, shipping, notification, gateway, web, infra"
  echo "See .agents/rules/commit-convention.md for complete guidelines."
  echo "------------------------------------------------------------------"
  echo ""
  exit 1
fi

exit 0
