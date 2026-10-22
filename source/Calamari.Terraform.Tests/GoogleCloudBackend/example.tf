terraform {
  required_providers {
    google = {
      source  = "hashicorp/google"
      version = "5.35.0"
    }
  }
}

resource "google_storage_bucket" "state" {
  name                        = "#{bucket_name}"
  project                     = "octopus-api-tester"
  location                    = "AUSTRALIA-SOUTHEAST1"
  force_destroy               = true
  uniform_bucket_level_access = true
}
