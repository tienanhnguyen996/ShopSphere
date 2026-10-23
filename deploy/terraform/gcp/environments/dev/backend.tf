terraform {
  required_version = ">= 1.5.0"

  required_providers {
    google = {
      source  = "hashicorp/google"
      version = "~> 5.20"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }

  # GCS remote state storage
  # Initialize with: terraform init -backend-config="bucket=<YOUR_TF_STATE_BUCKET>"
  backend "gcs" {
    prefix = "shopsphere/dev"
  }
}

provider "google" {
  project = var.project_id
  region  = var.region
}
