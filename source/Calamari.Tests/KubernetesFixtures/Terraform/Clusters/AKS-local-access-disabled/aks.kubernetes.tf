provider "kubernetes" {
  alias                  = "aks"
  host                   = azurerm_kubernetes_cluster.default.kube_config.0.host
  cluster_ca_certificate = base64decode(azurerm_kubernetes_cluster.default.kube_config.0.cluster_ca_certificate)
  client_certificate     = base64decode(azurerm_kubernetes_cluster.default.kube_config.0.client_certificate)
  client_key             = base64decode(azurerm_kubernetes_cluster.default.kube_config.0.client_key)
}
#
#resource "kubernetes_namespace" "default" {
#  provider = kubernetes.aks
#  metadata {
#    name      = var.test_namespace
#  }
#}
#
#resource "kubernetes_service_account" "default" {
#  provider = kubernetes.aks
#  metadata {
#    name      = "${random_pet.prefix.id}-account"
#    namespace = kubernetes_namespace.default.metadata.0.name
#  }
#}
#
#resource "kubernetes_secret" "default" {
#  provider = kubernetes.aks
#  metadata {
#    name        = "${kubernetes_service_account.default.metadata.0.name}-secret"
#    namespace   = kubernetes_namespace.default.metadata.0.name
#    annotations = {
#      "kubernetes.io/service-account.name" = kubernetes_service_account.default.metadata.0.name
#    }
#  }
#
#  type = "kubernetes.io/service-account-token"
#}
#
#resource "kubernetes_cluster_role" "default" {
#  provider = kubernetes.aks
#  metadata {
#    name      = "${random_pet.prefix.id}-role-account"
#  }
#
#  rule {
#    api_groups = ["*"]
#    resources  = ["*"]
#    verbs      = ["*"]
#  }
#}
#
#resource "kubernetes_cluster_role_binding" "default" {
#  provider = kubernetes.aks
#  metadata {
#    name      = "${random_pet.prefix.id}-role-account"
#  }
#  role_ref {
#    api_group = "rbac.authorization.k8s.io"
#    kind      = "ClusterRole"
#    name      = kubernetes_cluster_role.default.metadata.0.name
#  }
#  subject {
#    api_group = ""
#    kind = "ServiceAccount"
#    name = kubernetes_service_account.default.metadata.0.name
#    namespace = kubernetes_namespace.default.metadata.0.name
#  }
#}