resource "azurerm_resource_group" "default" {
  name     = "${var.static_resource_prefix}-rg"
  location = "Australia East"
}

resource "azurerm_kubernetes_cluster" "default" {
  name                = "${var.static_resource_prefix}-aks"
  resource_group_name = azurerm_resource_group.default.name
  location            = "Australia East"
  dns_prefix          = "${var.static_resource_prefix}-k8s"
  kubernetes_version  = "1.28"

  tags = {
    octopus-environment = "Staging"
    octopus-role        = "discovery-role"
    source              = "calamari-e2e-tests"
  }

  default_node_pool {
    name            = "default"
    vm_size         = "Standard_B2s"
    node_count      = 1
    os_disk_size_gb = 30
  }

  role_based_access_control_enabled = true

  service_principal {
    client_id     = var.aks_client_id
    client_secret = var.aks_client_secret
  }
}

resource "azurerm_kubernetes_cluster" "local_access_disabled" {
  name                = "${var.static_resource_prefix}-aks-no-local"
  resource_group_name = azurerm_resource_group.default.name
  location            = "Australia East"
  dns_prefix          = "${var.static_resource_prefix}-k8s-no-local"
  kubernetes_version  = "1.28"

  tags = {
    octopus-environment = "Staging"
    octopus-role        = "discovery-role"
    source              = "calamari-e2e-tests"
  }

  default_node_pool {
    name            = "default"
    vm_size         = "Standard_B2s"
    node_count      = 1
    os_disk_size_gb = 30
  }

  identity {
    type = "SystemAssigned"
  }

  role_based_access_control_enabled = true
  local_account_disabled            = true

  azure_active_directory_role_based_access_control {
    managed            = true
    azure_rbac_enabled = true
  }
}