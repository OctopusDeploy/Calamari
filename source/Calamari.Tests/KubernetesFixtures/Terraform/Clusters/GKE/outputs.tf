output "gke_cluster_endpoint" {
  description = "Endpoint for GKE control plane."
  value       = google_container_cluster.default.endpoint
}

output "gke_cluster_ca_certificate" {
  value     = base64decode(google_container_cluster.default.master_auth.0.cluster_ca_certificate)
  sensitive = true
}

output "gke_token" {
  value     = data.google_client_config.default.access_token
  sensitive = true
}

output "gke_cluster_name" {
  description = "GKE name."
  value       = google_container_cluster.default.name
}

output "gke_cluster_project" {
  description = "GKE clusters project."
  value       = google_container_cluster.default.project
}

output "gke_cluster_location" {
  description = "GKE clusters location."
  value       = google_container_cluster.default.location
}