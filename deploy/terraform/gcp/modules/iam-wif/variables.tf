variable "project_id" {
  type        = string
  description = "The GCP project ID."
}

variable "environment" {
  type        = string
  description = "Deployment environment (e.g. dev)."
  default     = "dev"
}

variable "github_repo" {
  type        = string
  description = "GitHub repository in format 'owner/repo' (e.g. 'tienanhnguyen996/ShopSphere')."
  default     = "tienanhnguyen996/ShopSphere"
}
