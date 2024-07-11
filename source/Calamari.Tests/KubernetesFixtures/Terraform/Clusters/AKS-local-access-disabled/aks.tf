data "azurerm_resource_group" "default" {
  name = "${var.static_resource_prefix}-rg"
}

data "azurerm_kubernetes_cluster" "default" {
  name                = "${var.static_resource_prefix}-aks-local-access-diabled"
  resource_group_name = data.azurerm_resource_group.default.name
}