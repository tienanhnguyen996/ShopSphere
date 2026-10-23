resource "google_iam_workload_identity_pool" "github_pool" {
  workload_identity_pool_id = "shopsphere-gh-pool-${var.environment}"
  display_name              = "GitHub Actions Pool (${var.environment})"
  description               = "Workload Identity Pool for GitHub Actions CI/CD"
  project                   = var.project_id
}

resource "google_iam_workload_identity_pool_provider" "github_provider" {
  workload_identity_pool_id          = google_iam_workload_identity_pool.github_pool.workload_identity_pool_id
  workload_identity_pool_provider_id = "shopsphere-gh-provider"
  display_name                       = "GitHub Actions Provider"
  description                        = "OIDC Provider for GitHub Actions"
  project                            = var.project_id

  attribute_mapping = {
    "google.subject"       = "assertion.sub"
    "attribute.actor"      = "assertion.actor"
    "attribute.repository" = "assertion.repository"
  }

  oidc {
    issuer_uri = "https://token.actions.githubusercontent.com"
  }
}

# Service Account for GitHub Actions CI/CD
resource "google_service_account" "github_actions_sa" {
  account_id   = "shopsphere-cicd-${var.environment}"
  display_name = "ShopSphere GitHub Actions CI/CD (${var.environment})"
  project      = var.project_id
}

# Bind GitHub repo to CI/CD Service Account
resource "google_service_account_iam_member" "wif_binding" {
  service_account_id = google_service_account.github_actions_sa.name
  role               = "roles/iam.workloadIdentityUser"
  member             = "principalSet://iam.googleapis.com/${google_iam_workload_identity_pool.github_pool.name}/attribute.repository/${var.github_repo}"
}

# Service Account for Gateway
resource "google_service_account" "gateway_sa" {
  account_id   = "shopsphere-gateway-${var.environment}"
  display_name = "ShopSphere Gateway Cloud Run Service Account (${var.environment})"
  project      = var.project_id
}

# Service Account for Downstream Microservices
resource "google_service_account" "services_sa" {
  account_id   = "shopsphere-services-${var.environment}"
  display_name = "ShopSphere Microservices Cloud Run Service Account (${var.environment})"
  project      = var.project_id
}

# Grant Observability roles to services SA
resource "google_project_iam_member" "services_trace" {
  project = var.project_id
  role    = "roles/cloudtrace.agent"
  member  = "serviceAccount:${google_service_account.services_sa.email}"
}

resource "google_project_iam_member" "services_logging" {
  project = var.project_id
  role    = "roles/logging.logWriter"
  member  = "serviceAccount:${google_service_account.services_sa.email}"
}

resource "google_project_iam_member" "services_monitoring" {
  project = var.project_id
  role    = "roles/monitoring.metricWriter"
  member  = "serviceAccount:${google_service_account.services_sa.email}"
}
