variable "project_id" {
  type        = string
  description = "The GCP project ID."
}

variable "environment" {
  type        = string
  description = "Deployment environment (e.g. dev)."
  default     = "dev"
}

variable "notification_email" {
  type        = string
  description = "Optional alert notification email address."
  default     = ""
}
