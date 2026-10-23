variable "project_id" {
  type        = string
  description = "The GCP project ID."
}

variable "region" {
  type        = string
  description = "The GCP region for subnets and Cloud NAT."
  default     = "us-central1"
}

variable "environment" {
  type        = string
  description = "Deployment environment (e.g. dev, staging, prod)."
  default     = "dev"
}

variable "network_name" {
  type        = string
  description = "Name of the VPC network."
  default     = "shopsphere-vpc"
}

variable "app_subnet_cidr" {
  type        = string
  description = "CIDR range for application and Cloud Run Direct VPC Egress subnet."
  default     = "10.0.1.0/24"
}

variable "data_subnet_cidr" {
  type        = string
  description = "CIDR range for internal data services (RabbitMQ VM, Cloud SQL, Memorystore)."
  default     = "10.0.2.0/24"
}
