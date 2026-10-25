variable "project_id" {
  type        = string
  description = "The GCP project ID to deploy resources into."
}

variable "region" {
  type        = string
  description = "The GCP region for all regional resources."
  default     = "asia-southeast1"
}

variable "zone" {
  type        = string
  description = "The GCP zone for single-VM resources (RabbitMQ)."
  default     = "asia-southeast1-a"
}

variable "environment" {
  type        = string
  description = "Environment name."
  default     = "dev"
}

variable "github_repo" {
  type        = string
  description = "GitHub repository (owner/repo) for Workload Identity Federation."
  default     = "tienanhnguyen996/ShopSphere"
}

variable "notification_email" {
  type        = string
  description = "Email address for monitoring alert notifications."
  default     = ""
}
