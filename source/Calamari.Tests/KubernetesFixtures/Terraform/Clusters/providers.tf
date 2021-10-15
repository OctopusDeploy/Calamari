terraform {
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "= 2.78.0" #Due to a bug with 2.79.x we are pinning to this version for now. https://github.com/Azure/AKS/issues/2584 
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
}

provider "google" {
  region      = "australia-southeast1"
  zone        = "australia-southeast1-c"
}
