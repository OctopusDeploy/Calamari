output "aks_cluster_host" {
  description = "Endpoint for AKS control plane."
  value       = azurerm_kubernetes_cluster.default.kube_config.0.host
  sensitive   = true
}


output "aks_cluster_username" {
  value     = azurerm_kubernetes_cluster.default.kube_config.0.username
  sensitive = true
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

output "aks_rg_id" {
  description = "Resource group Id"
  value       = azurerm_resource_group.default.id
}

output "aks_service_account_token" {
  value     = kubernetes_secret.default.data.token
  sensitive = true
}
