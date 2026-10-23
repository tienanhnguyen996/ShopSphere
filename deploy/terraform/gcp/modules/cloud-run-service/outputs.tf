output "service_name" {
  value       = google_cloud_run_v2_service.service.name
  description = "The name of the Cloud Run service."
}

output "service_id" {
  value       = google_cloud_run_v2_service.service.id
  description = "The ID of the Cloud Run service."
}

output "service_url" {
  value       = google_cloud_run_v2_service.service.uri
  description = "The URL of the Cloud Run service."
}
