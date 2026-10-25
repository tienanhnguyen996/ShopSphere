variable "project_id" {
  type        = string
  description = "The GCP project ID."
}

variable "region" {
  type        = string
  description = "The GCP region for the Redis instance."
  default     = "us-central1"
}

variable "environment" {
  type        = string
  description = "Deployment environment (e.g. dev)."
  default     = "dev"
}

variable "vpc_id" {
  type        = string
  description = "The ID of the VPC network for Memorystore Redis."
}

variable "memory_size_gb" {
  type        = number
  description = "Redis memory size in GiB."
  default     = 1
}

variable "tier" {
  type        = string
  description = "Redis tier: BASIC or STANDARD_HA."
  default     = "BASIC"
}
