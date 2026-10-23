# Persistent SSD disk for RabbitMQ state
resource "google_compute_disk" "rabbitmq_data" {
  name    = "shopsphere-rabbitmq-disk-${var.environment}"
  type    = "pd-balanced"
  zone    = var.zone
  size    = var.disk_size_gb
  project = var.project_id
}

# RabbitMQ Compute Engine VM
resource "google_compute_instance" "rabbitmq_vm" {
  name         = "shopsphere-rabbitmq-${var.environment}"
  machine_type = var.machine_type
  zone         = var.zone
  project      = var.project_id

  tags = ["shopsphere-rabbitmq", "internal-broker"]

  boot_disk {
    initialize_params {
      image = "ubuntu-os-cloud/ubuntu-2204-lts"
      size  = 20
      type  = "pd-standard"
    }
  }

  attached_disk {
    source      = google_compute_disk.rabbitmq_data.id
    device_name = "rabbitmq-data"
  }

  network_interface {
    subnetwork = var.data_subnet_id
    # No public IP assigned: strictly private
  }

  metadata = {
    enable-oslogin = "TRUE"
    startup-script = replace(
      file("${path.module}/scripts/startup.sh"),
      "$${RABBITMQ_ADMIN_PASSWORD}",
      var.admin_password
    )
  }

  service_account {
    scopes = ["cloud-platform"]
  }
}

# Firewall: AMQP & AMQPS from App Subnet only
resource "google_compute_firewall" "allow_amqp_internal" {
  name    = "shopsphere-allow-amqp-app-${var.environment}"
  network = var.vpc_id
  project = var.project_id

  allow {
    protocol = "tcp"
    ports    = ["5671", "5672"]
  }

  source_ranges = [var.app_subnet_cidr]
  target_tags   = ["shopsphere-rabbitmq"]
}

# Firewall: Management UI (15672) restricted strictly to Google IAP CIDR
resource "google_compute_firewall" "allow_rabbitmq_mgmt_iap" {
  name    = "shopsphere-allow-mgmt-iap-${var.environment}"
  network = var.vpc_id
  project = var.project_id

  allow {
    protocol = "tcp"
    ports    = ["15672", "22"]
  }

  source_ranges = ["35.235.240.0/20"]
  target_tags   = ["shopsphere-rabbitmq"]
}
