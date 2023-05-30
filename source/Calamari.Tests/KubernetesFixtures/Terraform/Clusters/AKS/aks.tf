resource "azurerm_resource_group" "default" {
  name     = "${random_pet.prefix.id}-rg"
  location = "Australia East"
}

resource "azurerm_kubernetes_cluster" "default" {
  name                = "${random_pet.prefix.id}-aks"
  resource_group_name = azurerm_resource_group.default.name
  location            = "Australia East"
  dns_prefix          = "${random_pet.prefix.id}-k8s"
  
  tags = {
    octopus-environment = "Staging"
    octopus-role = "discovery-role"
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
