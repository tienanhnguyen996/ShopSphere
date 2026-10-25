#!/usr/bin/env bash
# ShopSphere GCP Dev Teardown Script
# Runs terraform destroy against deploy/terraform/gcp/environments/dev

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

PROJECT_ID="${GCP_PROJECT_ID:-$(gcloud config get-value project 2>/dev/null)}"
if [ -z "$PROJECT_ID" ]; then
  echo -e "${RED}Error: GCP Project ID is required. Set GCP_PROJECT_ID or run 'gcloud config set project <id>'.${NC}"
  exit 1
fi

STATE_BUCKET="${GCP_TF_STATE_BUCKET:-shopsphere-tf-state-${PROJECT_ID}}"

echo -e "${RED}==========================================================${NC}"
echo -e "${RED}  DANGER: TEARDOWN SHOPSPHERE GCP DEV ENVIRONMENT${NC}"
echo -e "${RED}==========================================================${NC}"
echo -e "${YELLOW}Project ID:   ${PROJECT_ID}${NC}"
echo -e "${YELLOW}State Bucket: ${STATE_BUCKET}${NC}"
echo ""

if [ "$1" != "--force" ]; then
  read -r -p "Type 'DESTROY-ALL' to permanently delete all resources in this GCP environment: " CONFIRM
  if [ "$CONFIRM" != "DESTROY-ALL" ]; then
    echo -e "${GREEN}Destruction cancelled.${NC}"
    exit 0
  fi
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TF_DIR="${SCRIPT_DIR}/../deploy/terraform/gcp/environments/dev"

cd "$TF_DIR"

echo -e "\n${CYAN}Initializing Terraform...${NC}"
terraform init -backend-config="bucket=${STATE_BUCKET}"

echo -e "\n${RED}Destroying all infrastructure...${NC}"
terraform destroy -var="project_id=${PROJECT_ID}" -auto-approve

echo -e "\n${GREEN}[SUCCESS] All dev GCP resources destroyed.${NC}"
