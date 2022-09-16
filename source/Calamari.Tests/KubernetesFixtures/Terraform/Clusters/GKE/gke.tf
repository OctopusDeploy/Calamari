resource "google_container_cluster" "default" {
  name               = "${random_pet.prefix.id}-gke"
  project            = "octopus-api-tester"
  initial_node_count = 1
  node_config {
    preemptible  = true
    machine_type = "e2-medium"
  }
  master_auth {
    client_certificate_config {
      issue_client_certificate = true
    }
  }
}

data "google_client_config" "default" {
  depends_on = [google_container_cluster.default]
}