variable "project_id" {
  type        = string
  description = "The GCP project ID."
}

variable "region" {
  type        = string
  description = "The GCP region."
  default     = "us-central1"
}

variable "environment" {
  type        = string
  description = "Deployment environment (e.g. dev)."
  default     = "dev"
}

variable "gateway_service_name" {
  type        = string
  description = "The name of the Gateway Cloud Run service."
}

variable "domain_name" {
  type        = string
  description = "Optional custom domain name for SSL certificate (e.g. 'dev.shopsphere.io'). If empty, uses HTTP."
  default     = ""
}
