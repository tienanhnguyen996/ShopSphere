output "external_ip" {
  value       = google_compute_global_address.lb_ip.address
  description = "The public static IP address of the Global External Load Balancer."
}

output "spa_bucket_name" {
  value       = google_storage_bucket.spa_bucket.name
  description = "The name of the GCS bucket hosting the static React SPA."
}

output "security_policy_id" {
  value       = google_compute_security_policy.waf.id
  description = "The ID of the Cloud Armor security policy."
}
