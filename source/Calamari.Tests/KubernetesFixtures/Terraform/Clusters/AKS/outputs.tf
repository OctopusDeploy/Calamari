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

output "aks_cluster_fqdn" {
  description = "AKS Fully Qualified Domain Name"
  value = azurerm_kubernetes_cluster.default.fqdn
}

output "aks_rg_name" {
  description = "RG name."
  value       = azurerm_resource_group.default.name
}

output "aks_service_account_token" {
  value     = data.kubernetes_secret.default.data.token
  sensitive = true
}