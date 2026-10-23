output "db_secret_ids" {
  value = {
    for k, v in google_secret_manager_secret.db_conn : k => v.secret_id
  }
  description = "Map of microservice to Secret Manager DB connection secret ID."
}

output "rabbitmq_secret_ids" {
  value = {
    for k, v in google_secret_manager_secret.rabbitmq_conn : k => v.secret_id
  }
  description = "Map of microservice to Secret Manager RabbitMQ connection secret ID."
}

output "redis_auth_secret_id" {
  value       = google_secret_manager_secret.redis_auth.secret_id
  description = "Secret ID for Redis auth token."
}

output "jwt_private_key_secret_id" {
  value       = google_secret_manager_secret.jwt_private_key.secret_id
  description = "Secret ID for Identity RSA private key."
}

output "jwt_public_key_secret_id" {
  value       = google_secret_manager_secret.jwt_public_key.secret_id
  description = "Secret ID for Identity RSA public key."
}
