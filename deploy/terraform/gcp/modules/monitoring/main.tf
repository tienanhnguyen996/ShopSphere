# Email Notification Channel (if email provided)
resource "google_monitoring_notification_channel" "email" {
  count        = var.notification_email != "" ? 1 : 0
  display_name = "ShopSphere Dev Alerts (${var.environment})"
  type         = "email"
  project      = var.project_id

  labels = {
    email_address = var.notification_email
  }
}

# 1. Alert: Gateway 5xx Error Rate > 1%
resource "google_monitoring_alert_policy" "gateway_5xx_rate" {
  display_name = "ShopSphere Gateway 5xx Error Rate > 1% (${var.environment})"
  combiner     = "OR"
  project      = var.project_id

  conditions {
    display_name = "Cloud Run 5xx response ratio high"
    condition_threshold {
      filter          = "resource.type = \"cloud_run_revision\" AND metric.type = \"run.googleapis.com/request_count\" AND metric.labels.response_code_class = \"5xx\""
      duration        = "300s"
      comparison      = "COMPARISON_GT"
      threshold_value = 5

      aggregations {
        alignment_period   = "60s"
        per_series_aligner = "ALIGN_RATE"
      }
    }
  }

  notification_channels = var.notification_email != "" ? [google_monitoring_notification_channel.email[0].name] : []

  alert_strategy {
    auto_close = "1800s"
  }
}

# 2. Alert: Cloud SQL CPU Utilization > 80%
resource "google_monitoring_alert_policy" "cloud_sql_cpu" {
  display_name = "ShopSphere Cloud SQL CPU > 80% (${var.environment})"
  combiner     = "OR"
  project      = var.project_id

  conditions {
    display_name = "Cloud SQL CPU utilization > 80%"
    condition_threshold {
      filter          = "resource.type = \"cloudsql_database\" AND metric.type = \"cloudsql.googleapis.com/database/cpu/utilization\""
      duration        = "600s"
      comparison      = "COMPARISON_GT"
      threshold_value = 0.8

      aggregations {
        alignment_period   = "60s"
        per_series_aligner = "ALIGN_MEAN"
      }
    }
  }

  notification_channels = var.notification_email != "" ? [google_monitoring_notification_channel.email[0].name] : []
}

# 3. Alert: Cloud Run Container Memory Utilization > 85%
resource "google_monitoring_alert_policy" "cloud_run_memory" {
  display_name = "ShopSphere Cloud Run Memory > 85% (${var.environment})"
  combiner     = "OR"
  project      = var.project_id

  conditions {
    display_name = "Memory utilization > 85%"
    condition_threshold {
      filter          = "resource.type = \"cloud_run_revision\" AND metric.type = \"run.googleapis.com/container/memory/utilizations\""
      duration        = "300s"
      comparison      = "COMPARISON_GT"
      threshold_value = 0.85

      aggregations {
        alignment_period   = "60s"
        per_series_aligner = "ALIGN_MEAN"
      }
    }
  }

  notification_channels = var.notification_email != "" ? [google_monitoring_notification_channel.email[0].name] : []
}
