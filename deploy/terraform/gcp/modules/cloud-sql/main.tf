resource "random_id" "db_suffix" {
  byte_length = 4
}

resource "random_password" "root_password" {
  length           = 24
  special          = true
  override_special = "!#$%&*()-_=+[]{}<>:?"
}

resource "random_password" "service_passwords" {
  for_each         = var.services
  length           = 24
  special          = true
  override_special = "!#$%&*()-_=+[]{}<>:?"
}

resource "google_sql_database_instance" "instance" {
  name                = "shopsphere-sql-${var.environment}-${random_id.db_suffix.hex}"
  database_version    = var.database_version
  region              = var.region
  project             = var.project_id
  deletion_protection = false
  root_password       = random_password.root_password.result

  settings {
    tier              = var.tier
    availability_type = var.availability_type
    disk_type         = "PD_SSD"
    disk_size         = 50
    disk_autoresize   = true

    ip_configuration {
      ipv4_enabled    = false
      private_network = var.vpc_id
    }

    backup_configuration {
      enabled                        = true
      point_in_time_recovery_enabled = true
      start_time                     = "03:00"
    }

    database_flags {
      name  = "remote access"
      value = "on"
    }
  }

  depends_on = [var.service_networking_connection]
}

# Per-service databases
resource "google_sql_database" "databases" {
  for_each = var.services
  name     = each.value
  instance = google_sql_database_instance.instance.name
  project  = var.project_id
}

# Per-service SQL users
resource "google_sql_user" "users" {
  for_each = var.services
  name     = "${each.key}_user"
  instance = google_sql_database_instance.instance.name
  password = random_password.service_passwords[each.key].result
  project  = var.project_id
}
