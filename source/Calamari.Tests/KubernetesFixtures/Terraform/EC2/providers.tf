terraform {
  required_providers {

    aws = {
      source  = "hashicorp/aws"
      version = ">= 3.45.0"
    }
    tls = {
      source  = "hashicorp/tls"
      version = "3.1.0"
    }
    archive = {
      source  = "hashicorp/archive"
      version = "2.2.0"
    }
    http = {
      source  = "hashicorp/http"
      version = "2.1.0"
    }
    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = ">= 2.3.2"
    }
    template = {
      source = "hashicorp/template"
      version = "2.2.0"
    }
  }
  required_version = ">= 0.15"
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

provider "archive" {
}

provider "tls" {
}

provider "http" {
}

provider "template" {
}