provider "google" {

}

variable "bucket_name" {
  description = "the bucket name to use"
}

resource "google_storage_bucket" "mybucket" {
  name          = "${var.bucket_name}"
  location      = "AUSTRALIA-SOUTHEAST1"
  force_destroy = true
  project       = "octopus-api-tester"

  cors {
    origin          = ["*"]
    method          = ["GET", "HEAD", "PUT", "POST", "DELETE"]
    response_header = ["*"]
    max_age_seconds = 3600
  }
}

resource "google_storage_bucket_iam_binding" "binding" {
  bucket = google_storage_bucket.mybucket.name
  role = "roles/storage.objectViewer"
  members = [
    "allUsers"
  ]
}

resource "google_storage_bucket_object" "test" {
  name   = "test.txt"
  source = "test.txt"
  bucket = "${google_storage_bucket.mybucket.name}"
}

output "url" {
  value = "${google_storage_bucket_object.test.media_link}"
}