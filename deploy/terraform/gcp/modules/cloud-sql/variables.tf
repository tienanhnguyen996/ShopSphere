variable "project_id" {
  type        = string
  description = "The GCP project ID."
}

variable "region" {
  type        = string
  description = "The GCP region for the Cloud SQL instance."
  default     = "us-central1"
}

variable "environment" {
  type        = string
  description = "Deployment environment (e.g. dev)."
  default     = "dev"
}

variable "vpc_id" {
  type        = string
  description = "The ID of the VPC network for private IP access."
}

variable "service_networking_connection" {
  type        = any
  description = "Dependency anchor to ensure private VPC peering exists before Cloud SQL instance creation."
  default     = null
}

variable "database_version" {
  type        = string
  description = "SQL Server engine version."
  default     = "SQLSERVER_2022_WEB"
}

variable "tier" {
  type        = string
  description = "Machine tier for the Cloud SQL instance."
  default     = "db-custom-2-7680"
}

variable "availability_type" {
  type        = string
  description = "Availability type: ZONAL (for dev) or REGIONAL (for prod)."
  default     = "ZONAL"
}

variable "services" {
  type        = map(string)
  description = "Map of service key to database name."
  default = {
    catalog      = "ShopSphere.Catalog"
    cart         = "ShopSphere.Cart"
    identity     = "ShopSphere.Identity"
    inventory    = "ShopSphere.Inventory"
    payment      = "ShopSphere.Payments"
    order        = "ShopSphere.Orders"
    shipping     = "ShopSphere.Shipping"
    notification = "ShopSphere.Notifications"
  }
}
