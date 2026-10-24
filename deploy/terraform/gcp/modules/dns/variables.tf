variable "project_id" {
  type        = string
  description = "The GCP project ID."
}

variable "environment" {
  type        = string
  description = "Deployment environment."
  default     = "dev"
}

variable "vpc_id" {
  type        = string
  description = "The ID of the VPC network to bind the private DNS zone."
}

variable "domain_name" {
  type        = string
  description = "The DNS domain name ending with a dot."
  default     = "shopsphere.internal."
}

variable "rabbitmq_private_ip" {
  type        = string
  description = "Private IP address for the RabbitMQ host."
  default     = ""
}
