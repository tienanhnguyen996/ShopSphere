resource "google_redis_instance" "cache" {
  name                    = "shopsphere-redis-${var.environment}"
  tier                    = var.tier
  memory_size_gb          = var.memory_size_gb
  region                  = var.region
  project                 = var.project_id
  redis_version           = "REDIS_7_0"
  authorized_network      = var.vpc_id
  auth_enabled            = true
  transit_encryption_mode = "SERVER_AUTHENTICATION"

  maintenance_policy {
    weekly_maintenance_window {
      day = "SUNDAY"
      start_time {
        hours   = 3
        minutes = 0
        seconds = 0
        nanos   = 0
      }
    }
  }
}
