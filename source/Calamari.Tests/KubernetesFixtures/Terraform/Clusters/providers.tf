terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = ">= 2.63.0"
    }
    aws = {
      source  = "hashicorp/aws"
      version = ">= 3.45.0"
    }
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

provider "azurerm" {
  features {}
}

provider "aws" {
  default_tags {
    tags = {
      Team = "#team-steps"
      WorkloadName = "E2E-Test"
      ApplicationName = "Calamari"
      Criticality = "not-important"
    }
  }
}

provider "google" {
  region      = "australia-southeast1"
  zone        = "australia-southeast1-c"
}
