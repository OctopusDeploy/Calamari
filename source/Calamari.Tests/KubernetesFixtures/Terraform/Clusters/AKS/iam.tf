#data "azuread_service_principal" "calamari_test_runner" {
#  display_name = "Steps E2E Test Runner"
#}
#
#resource "azurerm_role_assignment" "cluster_user" {
#  scope                = azurerm_kubernetes_cluster.default.id
#  role_definition_name = "Azure Kubernetes Service Cluster User Role"
#  principal_id         = data.azuread_service_principal.calamari_test_runner.object_id
#}
#
#resource "azurerm_role_assignment" "cluster_admin" {
#  scope                = azurerm_kubernetes_cluster.default.id
#  role_definition_name = "Azure Kubernetes Service RBAC Cluster Admin"
#  principal_id         = data.azuread_service_principal.calamari_test_runner.object_id
#}
