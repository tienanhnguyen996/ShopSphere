resource "google_cloud_run_v2_service" "service" {
  name     = "shopsphere-${var.service_name}-${var.environment}"
  location = var.region
  project  = var.project_id
  ingress  = var.ingress

  lifecycle {
    ignore_changes = [
      client,
      client_version,
      template[0].containers[0].image
    ]
  }

  template {
    service_account       = var.service_account_email
    execution_environment = "EXECUTION_ENVIRONMENT_GEN2"

    scaling {
      min_instance_count = var.min_instance_count
      max_instance_count = var.max_instance_count
    }

    # Direct VPC Egress
    vpc_access {
      network_interfaces {
        network    = var.vpc_id
        subnetwork = var.app_subnet_id
      }
      egress = "ALL_TRAFFIC"
    }

    containers {
      image = var.image

      resources {
        limits = {
          cpu    = var.cpu
          memory = var.memory
        }
        cpu_idle = var.cpu_idle
      }

      ports {
        container_port = var.container_port
      }

      dynamic "env" {
        for_each = var.env_vars
        content {
          name  = env.key
          value = env.value
        }
      }

      dynamic "env" {
        for_each = var.secret_env_vars
        content {
          name = env.key
          value_source {
            secret_key_ref {
              secret  = env.value
              version = "latest"
            }
          }
        }
      }

      startup_probe {
        initial_delay_seconds = 10
        timeout_seconds       = 10
        period_seconds        = 10
        failure_threshold     = 20
        http_get {
          path = "/health"
          port = var.container_port
        }
      }

      liveness_probe {
        initial_delay_seconds = 20
        timeout_seconds       = 10
        period_seconds        = 15
        failure_threshold     = 5
        http_get {
          path = "/health"
          port = var.container_port
        }
      }
    }
  }
}

# IAM bindings for invokers (e.g. Gateway SA)
resource "google_cloud_run_v2_service_iam_member" "invokers" {
  count    = length(var.invoker_members)
  project  = var.project_id
  location = var.region
  name     = google_cloud_run_v2_service.service.name
  role     = "roles/run.invoker"
  member   = var.invoker_members[count.index]
}
