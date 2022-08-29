terraform {
  required_providers {
    google = {
      source  = "hashicorp/google"
      version = "3.71.0"
    }
    archive = {
      source  = "hashicorp/archive"
      version = "2.2.0"
    }
    tls = {
      source  = "hashicorp/tls"
      version = "3.1.0"
    }
    http = {
      source  = "hashicorp/http"
      version = "2.1.0"
    }
  }
  required_version = ">= 0.15"
}

provider "google" {
  region      = "australia-southeast1"
  zone        = "australia-southeast1-c"
}

provider "archive" {
}

provider "tls" {
}

provider "http" {
}
