output "aks_cluster_host" {
  description = "Endpoint for AKS control plane."
  value       = data.azurerm_kubernetes_cluster.local_access_disabled.kube_config.0.host
  sensitive   = true
}


output "aks_cluster_username" {
  value     = data.azurerm_kubernetes_cluster.local_access_disabled.kube_config.0.username
  sensitive = true
}

output "aks_cluster_password" {
  value     = data.azurerm_kubernetes_cluster.local_access_disabled.kube_config.0.password
  sensitive = true
}

output "aks_cluster_client_certificate" {
  value     = base64decode(data.azurerm_kubernetes_cluster.local_access_disabled.kube_config.0.client_certificate)
  sensitive = true
}

output "aks_cluster_client_key" {
  value     = base64decode(data.azurerm_kubernetes_cluster.local_access_disabled.kube_config.0.client_key)
  sensitive = true
}

output "aks_cluster_ca_certificate" {
  value     = base64decode(data.azurerm_kubernetes_cluster.local_access_disabled.kube_config.0.cluster_ca_certificate)
  sensitive = true
}

output "aks_cluster_name" {
  description = "AKS name."
  value       = data.azurerm_kubernetes_cluster.local_access_disabled.name
}

output "aks_rg_name" {
  description = "RG name."
  value       = data.azurerm_resource_group.default.name
}