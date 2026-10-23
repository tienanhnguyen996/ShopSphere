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

variable "service_name" {
  type        = string
  description = "Name of the microservice or gateway."
}

variable "image" {
  type        = string
  description = "Container image URI in Artifact Registry."
}

variable "container_port" {
  type        = number
  description = "Port the container listens on."
  default     = 8080
}

variable "service_account_email" {
  type        = string
  description = "Service account email under which the Cloud Run service runs."
}

variable "ingress" {
  type        = string
  description = "Cloud Run ingress traffic setting (INTERNAL_LOAD_BALANCER or INTERNAL_ONLY)."
  default     = "INGRESS_TRAFFIC_INTERNAL_ONLY"
}

variable "vpc_id" {
  type        = string
  description = "VPC network ID for Direct VPC Egress."
}

variable "app_subnet_id" {
  type        = string
  description = "Subnet ID for Direct VPC Egress."
}

variable "min_instance_count" {
  type        = number
  description = "Minimum running container instances."
  default     = 1
}

variable "max_instance_count" {
  type        = number
  description = "Maximum running container instances."
  default     = 5
}

variable "cpu_idle" {
  type        = bool
  description = "Whether CPU is throttled when not processing requests. Set false for background worker liveness."
  default     = false
}

variable "cpu" {
  type        = string
  description = "vCPU allocation (e.g. '1', '2')."
  default     = "1"
}

variable "memory" {
  type        = string
  description = "Memory allocation (e.g. '512Mi', '1Gi')."
  default     = "512Mi"
}

variable "env_vars" {
  type        = map(string)
  description = "Plain text environment variables."
  default     = {}
}

variable "secret_env_vars" {
  type        = map(string)
  description = "Map of environment variable name to Secret Manager secret ID."
  default     = {}
}

variable "invoker_members" {
  type        = list(string)
  description = "List of IAM members granted roles/run.invoker (e.g. Gateway SA)."
  default     = []
}
