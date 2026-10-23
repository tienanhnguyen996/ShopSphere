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

variable "job_name" {
  type        = string
  description = "Name identifier for the migration job (e.g. 'catalog-migrate')."
}

variable "image" {
  type        = string
  description = "Container image containing the compiled EF Core migration bundle."
}

variable "service_account_email" {
  type        = string
  description = "Service account email executing the migration job."
}

variable "vpc_id" {
  type        = string
  description = "VPC network ID for Direct VPC Egress to reach Cloud SQL."
}

variable "app_subnet_id" {
  type        = string
  description = "Subnet ID for Direct VPC Egress."
}

variable "secret_connection_string_id" {
  type        = string
  description = "Secret Manager secret ID containing the database connection string."
}
