# VPC
resource "google_compute_network" "vpc" {
  name                    = "${random_pet.prefix.id}-vpc"
  auto_create_subnetworks = "false"
}

# Subnet
resource "google_compute_subnetwork" "subnet" {
  name          = "${random_pet.prefix.id}-subnet"
  region        = local.region
  network       = google_compute_network.vpc.name
  ip_cidr_range = "10.10.0.0/24"
}

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