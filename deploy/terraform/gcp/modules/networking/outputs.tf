output "vpc_id" {
  value       = google_compute_network.vpc.id
  description = "The ID of the VPC network."
}

output "vpc_name" {
  value       = google_compute_network.vpc.name
  description = "The name of the VPC network."
}

output "app_subnet_id" {
  value       = google_compute_subnetwork.app_subnet.id
  description = "The ID of the application subnet."
}

output "app_subnet_name" {
  value       = google_compute_subnetwork.app_subnet.name
  description = "The name of the application subnet."
}

output "data_subnet_id" {
  value       = google_compute_subnetwork.data_subnet.id
  description = "The ID of the data subnet."
}

output "data_subnet_name" {
  value       = google_compute_subnetwork.data_subnet.name
  description = "The name of the data subnet."
}

output "service_networking_connection" {
  value       = google_service_networking_connection.private_vpc_connection.id
  description = "The service networking connection resource ID for Cloud SQL."
}
