variable "project_id" {
  type        = string
  description = "The GCP project ID."
}

variable "region" {
  type        = string
  description = "The GCP region for the registry."
  default     = "us-central1"
}

variable "repository_id" {
  type        = string
  description = "The ID of the Artifact Registry repository."
  default     = "shopsphere-containers"
}
