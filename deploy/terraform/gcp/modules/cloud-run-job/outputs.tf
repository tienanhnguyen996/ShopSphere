output "job_name" {
  value       = google_cloud_run_v2_job.migration_job.name
  description = "The name of the migration job."
}

output "job_id" {
  value       = google_cloud_run_v2_job.migration_job.id
  description = "The ID of the migration job."
}
