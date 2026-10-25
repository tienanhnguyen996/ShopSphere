<#
.SYNOPSIS
    Tears down all ShopSphere GCP Dev environment infrastructure.
.DESCRIPTION
    Runs terraform destroy against deploy/terraform/gcp/environments/dev.
    Requires manual confirmation unless -Force is specified.
.PARAMETER ProjectId
    GCP Project ID. If omitted, will try to read from gcloud config or $env:GCP_PROJECT_ID.
.PARAMETER StateBucket
    GCS Terraform state bucket name.
.PARAMETER Force
    Skip confirmation prompt.
#>
param (
    [Parameter(Mandatory=$false)]
    [string]$ProjectId = $env:GCP_PROJECT_ID,

    [Parameter(Mandatory=$false)]
    [string]$StateBucket = $env:GCP_TF_STATE_BUCKET,

    [Parameter(Mandatory=$false)]
    [switch]$Force
)

$ErrorActionPreference = "Stop"

if (-not $ProjectId) {
    $ProjectId = (gcloud config get-value project 2>$null)
}

if (-not $ProjectId) {
    Write-Error "GCP Project ID must be provided via -ProjectId, GCP_PROJECT_ID env var, or active gcloud config."
    exit 1
}

if (-not $StateBucket) {
    $StateBucket = "shopsphere-tf-state-$ProjectId"
}

Write-Host "==========================================================" -ForegroundColor Red
Write-Host "  DANGER: TEARDOWN SHOPSPHERE GCP DEV ENVIRONMENT" -ForegroundColor Red
Write-Host "==========================================================" -ForegroundColor Red
Write-Host "Project ID:   $ProjectId" -ForegroundColor Yellow
Write-Host "State Bucket: $StateBucket" -ForegroundColor Yellow
Write-Host ""

if (-not $Force) {
    $confirm = Read-Host "Type 'DESTROY-ALL' to permanently delete all resources in this GCP environment"
    if ($confirm -ne "DESTROY-ALL") {
        Write-Host "Destruction cancelled." -ForegroundColor Green
        exit 0
    }
}

$terraformDir = Join-Path $PSScriptRoot "..\deploy\terraform\gcp\environments\dev"
Push-Location $terraformDir
try {
    Write-Host "`nInitializing Terraform..." -ForegroundColor Cyan
    terraform init -backend-config="bucket=$StateBucket"

    Write-Host "`nDestroying all infrastructure..." -ForegroundColor Red
    terraform destroy -var="project_id=$ProjectId" -auto-approve

    Write-Host "`n[SUCCESS] All dev GCP resources destroyed." -ForegroundColor Green
}
finally {
    Pop-Location
}
