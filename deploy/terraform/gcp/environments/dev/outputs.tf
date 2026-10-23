output "external_load_balancer_ip" {
  value       = module.load_balancer.external_ip
  description = "Public static IP for ShopSphere (Gateway & Web SPA)."
}

output "artifact_registry_url" {
  value       = module.artifact_registry.repository_url
  description = "Artifact Registry Docker repository URL."
}

output "wif_provider_name" {
  value       = module.iam_wif.wif_provider_name
  description = "Workload Identity Provider resource name for GitHub Actions."
}

output "cicd_service_account" {
  value       = module.iam_wif.cicd_service_account_email
  description = "CI/CD Service Account email."
}

output "cloud_sql_private_ip" {
  value       = module.cloud_sql.private_ip_address
  description = "Private IP of Cloud SQL SQL Server instance."
}

output "redis_private_ip" {
  value       = module.memorystore.host
  description = "Private IP of Memorystore Redis instance."
}

output "rabbitmq_private_ip" {
  value       = module.rabbitmq.private_ip
  description = "Private IP of RabbitMQ Compute Engine VM."
}

output "spa_storage_bucket" {
  value       = module.load_balancer.spa_bucket_name
  description = "GCS bucket name for React SPA static assets."
}
