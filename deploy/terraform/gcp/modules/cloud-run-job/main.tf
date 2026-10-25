resource "google_cloud_run_v2_job" "migration_job" {
  name     = "shopsphere-${var.job_name}-${var.environment}"
  location = var.region
  project  = var.project_id

  lifecycle {
    ignore_changes = [
      template[0].template[0].containers[0].image
    ]
  }

  template {
    template {
      service_account = var.service_account_email
      max_retries     = 1
      timeout         = "600s"

      # Direct VPC Egress to reach Cloud SQL private IP
      vpc_access {
        network_interfaces {
          network    = var.vpc_id
          subnetwork = var.app_subnet_id
        }
        egress = "PRIVATE_RANGES_ONLY"
      }

      containers {
        image = var.image

        resources {
          limits = {
            cpu    = "1"
            memory = "512Mi"
          }
        }

        env {
          name = "DB_CONNECTION_STRING"
          value_source {
            secret_key_ref {
              secret  = var.secret_connection_string_id
              version = "latest"
            }
          }
        }

        # EF Core bundle takes connection string argument
        args = ["--connection", "$(DB_CONNECTION_STRING)"]
      }
    }
  }
}
