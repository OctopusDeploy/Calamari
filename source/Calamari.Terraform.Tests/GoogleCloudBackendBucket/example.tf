provider "google" {
}

variable "bucket_name" {
  description = "the bucket name to use for remote state"
}

resource "google_storage_bucket" "state" {
  name                        = "${var.bucket_name}"
  location                    = "AUSTRALIA-SOUTHEAST1"
  force_destroy               = true
  uniform_bucket_level_access = true
  project                     = "octopus-api-tester"
}
