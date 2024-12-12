data "google_container_cluster" "default" {
  name    = "${var.static_resource_prefix}-gke"
  project = "octopus-api-tester"
}

data "google_client_config" "default" {
  depends_on = [data.google_container_cluster.default]
}