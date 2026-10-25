output "gateway_alert_policy_id" {
  value       = google_monitoring_alert_policy.gateway_5xx_rate.id
  description = "Alert policy ID for Gateway 5xx rate."
}

output "cloud_sql_alert_policy_id" {
  value       = google_monitoring_alert_policy.cloud_sql_cpu.id
  description = "Alert policy ID for Cloud SQL CPU utilization."
}
