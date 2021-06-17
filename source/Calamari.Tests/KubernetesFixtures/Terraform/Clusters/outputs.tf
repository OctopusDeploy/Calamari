output "eks_client_id" {
  value = aws_iam_access_key.default.id
}

output "eks_secret_key" {
  value     = aws_iam_access_key.default.secret
  sensitive = true
}

output "eks_cluster_endpoint" {
  description = "Endpoint for EKS control plane."
  value       = aws_eks_cluster.default.endpoint
}

output "eks_cluster_ca_certificate" {
  value     = base64decode(aws_eks_cluster.default.certificate_authority[0].data)
  sensitive = true
}

output "eks_cluster_name" {
  description = "EKS name."
  value       = aws_eks_cluster.default.name
}

output "aws_vpc_id" {
  value = aws_vpc.default.id
}

output "aws_subnet_id" {
  value = aws_subnet.default[0].id
}

output "aws_iam_instance_profile_name" {
  value = aws_iam_instance_profile.profile.name
}

output "aks_cluster_host" {
  description = "Endpoint for AKS control plane."
  value       = azurerm_kubernetes_cluster.default.kube_config.0.host
}


output "aks_cluster_username" {
  value = azurerm_kubernetes_cluster.default.kube_config.0.username
}

output "aks_cluster_password" {
  value     = azurerm_kubernetes_cluster.default.kube_config.0.password
  sensitive = true
}

output "aks_cluster_client_certificate" {
  value     = base64decode(azurerm_kubernetes_cluster.default.kube_config.0.client_certificate)
  sensitive = true
}

output "aks_cluster_client_key" {
  value     = base64decode(azurerm_kubernetes_cluster.default.kube_config.0.client_key)
  sensitive = true
}

output "aks_cluster_ca_certificate" {
  value     = base64decode(azurerm_kubernetes_cluster.default.kube_config.0.cluster_ca_certificate)
  sensitive = true
}

output "aks_cluster_name" {
  description = "AKS name."
  value       = azurerm_kubernetes_cluster.default.name
}

output "aks_rg_name" {
  description = "RG name."
  value       = azurerm_resource_group.default.name
}

output "aks_service_account_token" {
  value     = data.kubernetes_secret.default.data.token
  sensitive = true
}

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
