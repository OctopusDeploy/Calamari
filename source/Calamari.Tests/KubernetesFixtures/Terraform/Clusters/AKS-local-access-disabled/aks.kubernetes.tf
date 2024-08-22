provider "kubernetes" {
  alias                  = "aks"
  host                   = data.azurerm_kubernetes_cluster.local_access_disabled.kube_config.0.host
  cluster_ca_certificate = base64decode(data.azurerm_kubernetes_cluster.local_access_disabled.kube_config.0.cluster_ca_certificate)
  client_certificate     = base64decode(data.azurerm_kubernetes_cluster.local_access_disabled.kube_config.0.client_certificate)
  client_key             = base64decode(data.azurerm_kubernetes_cluster.local_access_disabled.kube_config.0.client_key)
}