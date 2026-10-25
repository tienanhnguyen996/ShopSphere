resource "google_dns_managed_zone" "private_zone" {
  name        = "shopsphere-private-zone-${var.environment}"
  dns_name    = var.domain_name
  description = "Private DNS zone for ShopSphere internal service discovery"
  visibility  = "private"
  project     = var.project_id

  private_visibility_config {
    networks {
      network_url = var.vpc_id
    }
  }
}

resource "google_dns_record_set" "rabbitmq" {
  count        = var.rabbitmq_private_ip != "" ? 1 : 0
  name         = "rabbitmq.${google_dns_managed_zone.private_zone.dns_name}"
  managed_zone = google_dns_managed_zone.private_zone.name
  type         = "A"
  ttl          = 300
  rrdatas      = [var.rabbitmq_private_ip]
  project      = var.project_id
}
