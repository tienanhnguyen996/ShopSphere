output "wif_provider_name" {
  value       = google_iam_workload_identity_pool_provider.github_provider.name
  description = "Full resource name of the Workload Identity Provider."
}

output "cicd_service_account_email" {
  value       = google_service_account.github_actions_sa.email
  description = "Email of the GitHub Actions CI/CD service account."
}

output "gateway_service_account_email" {
  value       = google_service_account.gateway_sa.email
  description = "Email of the API Gateway Cloud Run service account."
}

output "services_service_account_email" {
  value       = google_service_account.services_sa.email
  description = "Email of the microservices Cloud Run service account."
}
