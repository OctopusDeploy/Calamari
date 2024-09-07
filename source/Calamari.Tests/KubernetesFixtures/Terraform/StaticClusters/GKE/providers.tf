terraform {
  required_providers {
    google = {
      source  = "hashicorp/google"
      version = "3.71.0"
    }
    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = ">= 2.3.2"
    }
  }
  required_version = ">= 0.15"
}

locals {
  region = "australia-southeast1"
}

provider "google" {
  region = local.region
  zone   = "australia-southeast1-c"
}