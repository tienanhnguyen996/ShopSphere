output "zone_name" {
  value       = google_dns_managed_zone.private_zone.name
  description = "The name of the private DNS managed zone."
}

output "dns_name" {
  value       = google_dns_managed_zone.private_zone.dns_name
  description = "The base domain name of the private DNS zone."
}

output "rabbitmq_fqdn" {
  value       = "rabbitmq.${trimsuffix(google_dns_managed_zone.private_zone.dns_name, ".")}"
  description = "The fully qualified domain name for RabbitMQ."
}
