variable "project_id" {
  type        = string
  description = "The GCP project ID."
}

variable "region" {
  type        = string
  description = "The GCP region."
  default     = "us-central1"
}

variable "zone" {
  type        = string
  description = "The GCP zone for the single VM."
  default     = "us-central1-a"
}

variable "environment" {
  type        = string
  description = "Deployment environment (e.g. dev)."
  default     = "dev"
}

variable "vpc_id" {
  type        = string
  description = "VPC network ID."
}

variable "data_subnet_id" {
  type        = string
  description = "Subnet ID where RabbitMQ VM is placed."
}

variable "app_subnet_cidr" {
  type        = string
  description = "CIDR range of the App Subnet allowed to access AMQP."
  default     = "10.0.1.0/24"
}

variable "machine_type" {
  type        = string
  description = "Compute Engine machine type for dev."
  default     = "e2-medium"
}

variable "disk_size_gb" {
  type        = number
  description = "Persistent disk size for RabbitMQ storage."
  default     = 20
}

variable "admin_password" {
  type        = string
  sensitive   = true
  description = "Admin password for RabbitMQ."
}
