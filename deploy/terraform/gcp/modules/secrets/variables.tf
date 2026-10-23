variable "project_id" {
  type        = string
  description = "The GCP project ID."
}

variable "environment" {
  type        = string
  description = "Deployment environment (e.g. dev)."
  default     = "dev"
}

variable "services" {
  type        = list(string)
  description = "List of microservice names requiring database & messaging secrets."
  default = [
    "catalog",
    "cart",
    "identity",
    "inventory",
    "payment",
    "order",
    "shipping",
    "notification"
  ]
}
