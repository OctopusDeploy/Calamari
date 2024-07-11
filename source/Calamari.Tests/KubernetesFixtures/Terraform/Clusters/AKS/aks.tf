data "azurerm_resource_group" "default" {
  name = "${var.static_resource_prefix}-rg"
}

data "azurerm_kubernetes_cluster" "default" {
  name                = "${var.static_resource_prefix}-aks"
  resource_group_name = data.azurerm_resource_group.default.name
}
