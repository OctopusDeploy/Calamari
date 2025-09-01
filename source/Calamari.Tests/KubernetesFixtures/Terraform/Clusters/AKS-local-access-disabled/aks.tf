data "azurerm_resource_group" "default" {
  name = "${var.static_resource_prefix}-rg"
}

data "azurerm_kubernetes_cluster" "local_access_disabled" {
  name                = "${var.static_resource_prefix}-aks-no-local"
  resource_group_name = data.azurerm_resource_group.default.name
}