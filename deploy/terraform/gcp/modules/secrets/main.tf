# Per-service database connection strings
resource "google_secret_manager_secret" "db_conn" {
  for_each  = toset(var.services)
  secret_id = "shopsphere-${each.key}-db-conn-${var.environment}"
  project   = var.project_id

  replication {
    auto {}
  }
}

# Per-service RabbitMQ connection strings
resource "google_secret_manager_secret" "rabbitmq_conn" {
  for_each  = toset(var.services)
  secret_id = "shopsphere-${each.key}-rabbitmq-conn-${var.environment}"
  project   = var.project_id

  replication {
    auto {}
  }
}

# RabbitMQ admin password
resource "google_secret_manager_secret" "rabbitmq_admin_pass" {
  secret_id = "shopsphere-rabbitmq-admin-pass-${var.environment}"
  project   = var.project_id

  replication {
    auto {}
  }
}

# Redis Auth token
resource "google_secret_manager_secret" "redis_auth" {
  secret_id = "shopsphere-redis-auth-${var.environment}"
  project   = var.project_id

  replication {
    auto {}
  }
}

# Identity RSA private key
resource "google_secret_manager_secret" "jwt_private_key" {
  secret_id = "shopsphere-identity-jwt-private-key-${var.environment}"
  project   = var.project_id

  replication {
    auto {}
  }
}

# Identity RSA public key
resource "google_secret_manager_secret" "jwt_public_key" {
  secret_id = "shopsphere-identity-jwt-public-key-${var.environment}"
  project   = var.project_id

  replication {
    auto {}
  }
}
