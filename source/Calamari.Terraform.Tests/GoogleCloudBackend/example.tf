terraform {
  # Configured with -backend-config so the bucket can be created by the test
  backend "gcs" {}
}

output "message" {
  value = "Hello from a gcs backend"
}
