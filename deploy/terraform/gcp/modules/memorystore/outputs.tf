output "host" {
  value       = google_redis_instance.cache.host
  description = "Private IP address of the Memorystore Redis instance."
}

output "port" {
  value       = google_redis_instance.cache.port
  description = "Port number of the Redis instance."
}

output "auth_string" {
  value       = google_redis_instance.cache.auth_string
  sensitive   = true
  description = "AUTH string for Redis authentication."
}

output "connection_string" {
  value       = "${google_redis_instance.cache.host}:${google_redis_instance.cache.port},password=${google_redis_instance.cache.auth_string},ssl=true"
  sensitive   = true
  description = "StackExchange.Redis formatted connection string with TLS and Auth."
}
