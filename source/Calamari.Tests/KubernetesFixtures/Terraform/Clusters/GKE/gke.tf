# Get latest version
data "google_container_engine_versions" "main" {
  location = local.region
  project       = "octopus-api-tester"

  # Since this is just a string match, it's recommended that you append a . after minor versions 
  # to ensure that prefixes such as 1.1 don't match versions like 1.12.5-gke.10 accidentally.
  version_prefix = "1.28."
}

# VPC
resource "google_compute_network" "vpc" {
  name                    = "${random_pet.prefix.id}-vpc"
  auto_create_subnetworks = "false"
  project                 = "octopus-api-tester"
}

# Subnet
resource "google_compute_subnetwork" "subnet" {
  name          = "${random_pet.prefix.id}-subnet"
  region        = local.region
  network       = google_compute_network.vpc.name
  ip_cidr_range = "10.10.0.0/24"
  project       = "octopus-api-tester"
}

locals {
  # we will pick the latest k8s version
  master_version = data.google_container_engine_versions.main.valid_master_versions[0]
}

resource "google_container_cluster" "default" {
  name               = "${random_pet.prefix.id}-gke"
  project            = "octopus-api-tester"
  min_master_version = local.master_version
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
  
  # to prevent automatic updates to cluster
  release_channel {
    channel = "UNSPECIFIED"
  }

  network    = google_compute_network.vpc.name
  subnetwork = google_compute_subnetwork.subnet.name
}

data "google_client_config" "default" {
  depends_on = [google_container_cluster.default]
}