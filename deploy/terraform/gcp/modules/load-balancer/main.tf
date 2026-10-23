# Cloud Armor Security Policy with WAF and Rate Limiting
resource "google_compute_security_policy" "waf" {
  name        = "shopsphere-waf-policy-${var.environment}"
  description = "Cloud Armor security policy with OWASP Top 10 and rate limiting"
  project     = var.project_id

  # 1. SQL Injection protection
  rule {
    action   = "deny(403)"
    priority = "1000"
    match {
      expr {
        expression = "evaluatePreconfiguredExpr('sqli-v33-stable')"
      }
    }
    description = "Deny SQL injection attempts"
  }

  # 2. Cross-Site Scripting (XSS) protection
  rule {
    action   = "deny(403)"
    priority = "1100"
    match {
      expr {
        expression = "evaluatePreconfiguredExpr('xss-v33-stable')"
      }
    }
    description = "Deny XSS attempts"
  }

  # 3. Remote Code Execution (RCE) protection
  rule {
    action   = "deny(403)"
    priority = "1200"
    match {
      expr {
        expression = "evaluatePreconfiguredExpr('rce-v33-stable')"
      }
    }
    description = "Deny RCE attempts"
  }

  # 4. Local File Inclusion (LFI) protection
  rule {
    action   = "deny(403)"
    priority = "1300"
    match {
      expr {
        expression = "evaluatePreconfiguredExpr('lfi-v33-stable')"
      }
    }
    description = "Deny LFI attempts"
  }

  # 5. IP Rate limiting (100 req / minute per client IP)
  rule {
    action   = "throttle"
    priority = "2000"
    match {
      versioned_expr = "SRC_IPS_V1"
      config {
        src_ip_ranges = ["*"]
      }
    }
    rate_limit_options {
      conform_action = "allow"
      exceed_action  = "deny(429)"
      enforce_on_key = "IP"
      rate_limit_threshold {
        count        = 100
        interval_sec = 60
      }
    }
    description = "Throttle clients exceeding 100 requests per minute"
  }

  # Default rule: Allow legitimate traffic
  rule {
    action   = "allow"
    priority = "2147483647"
    match {
      versioned_expr = "SRC_IPS_V1"
      config {
        src_ip_ranges = ["*"]
      }
    }
    description = "Default allow rule"
  }
}

# Static Frontend SPA Storage Bucket
resource "google_storage_bucket" "spa_bucket" {
  name                        = "shopsphere-spa-${var.environment}-${var.project_id}"
  location                    = var.region
  project                     = var.project_id
  force_destroy               = true
  uniform_bucket_level_access = true

  website {
    main_page_suffix = "index.html"
    not_found_page   = "index.html"
  }
}

# Public read access for SPA static assets
resource "google_storage_bucket_iam_member" "spa_public_read" {
  bucket = google_storage_bucket.spa_bucket.name
  role   = "roles/storage.objectViewer"
  member = "allUsers"
}

# Cloud CDN Backend Bucket for SPA
resource "google_compute_backend_bucket" "spa_backend" {
  name        = "shopsphere-spa-backend-${var.environment}"
  bucket_name = google_storage_bucket.spa_bucket.name
  enable_cdn  = true
  project     = var.project_id

  cdn_policy {
    cache_mode        = "CACHE_ALL_STATIC"
    default_ttl       = 3600
    client_ttl        = 3600
    max_ttl           = 86400
    negative_caching  = true
    serve_while_stale = 86400
  }
}

# Serverless Network Endpoint Group (NEG) for Gateway Cloud Run
resource "google_compute_region_network_endpoint_group" "gateway_neg" {
  name                  = "shopsphere-gateway-neg-${var.environment}"
  network_endpoint_type = "SERVERLESS"
  region                = var.region
  project               = var.project_id

  cloud_run {
    service = var.gateway_service_name
  }
}

# Backend Service for Gateway with Cloud Armor attached
resource "google_compute_backend_service" "gateway_backend" {
  name            = "shopsphere-gateway-backend-${var.environment}"
  protocol        = "HTTP"
  port_name       = "http"
  timeout_sec     = 30
  security_policy = google_compute_security_policy.waf.id
  project         = var.project_id

  backend {
    group = google_compute_region_network_endpoint_group.gateway_neg.id
  }
}

# URL Map: Route /api/* to Gateway, everything else to SPA
resource "google_compute_url_map" "url_map" {
  name            = "shopsphere-url-map-${var.environment}"
  default_service = google_compute_backend_bucket.spa_backend.id
  project         = var.project_id

  host_rule {
    hosts        = ["*"]
    path_matcher = "allpaths"
  }

  path_matcher {
    name            = "allpaths"
    default_service = google_compute_backend_bucket.spa_backend.id

    path_rule {
      paths   = ["/api/*", "/health", "/swagger/*"]
      service = google_compute_backend_service.gateway_backend.id
    }
  }
}

# Global Static IP
resource "google_compute_global_address" "lb_ip" {
  name    = "shopsphere-lb-ip-${var.environment}"
  project = var.project_id
}

# HTTP Target Proxy & Forwarding Rule
resource "google_compute_target_http_proxy" "http_proxy" {
  name    = "shopsphere-http-proxy-${var.environment}"
  url_map = google_compute_url_map.url_map.id
  project = var.project_id
}

resource "google_compute_global_forwarding_rule" "http_rule" {
  name       = "shopsphere-http-rule-${var.environment}"
  target     = google_compute_target_http_proxy.http_proxy.id
  port_range = "80"
  ip_address = google_compute_global_address.lb_ip.address
  project    = var.project_id
}
