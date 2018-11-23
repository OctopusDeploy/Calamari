resource "azurerm_resource_group" "resgrp" {
  name     = "terraformtestrg"
  location = "AustraliaSouthEast"
}

resource "azurerm_storage_account" "storageaccount" {
  name                     = "terraformtestaccount"
  resource_group_name      = "${azurerm_resource_group.resgrp.name}"
  location                 = "${azurerm_resource_group.resgrp.location}"
  account_tier             = "Standard"
  account_kind			   = "BlobStorage"
  account_replication_type = "LRS"
}

resource "azurerm_storage_container" "blobstorage" {
  name                  = "terraformtestcontainer"
  resource_group_name   = "${azurerm_resource_group.resgrp.name}"
  storage_account_name  = "${azurerm_storage_account.storageaccount.name}"
  container_access_type = "blob"
}

resource "azurerm_storage_blob" "blobobject" {
  depends_on             = ["azurerm_storage_container.blobstorage"]
  name                   = "test.txt"
  resource_group_name    = "${azurerm_resource_group.resgrp.name}"
  storage_account_name   = "${azurerm_storage_account.storageaccount.name}"
  storage_container_name = "${azurerm_storage_container.blobstorage.name}"
  type                   = "block"
  source                 = "test.txt"
}

output "url" {
  value = "http://${azurerm_storage_account.storageaccount.name}.blob.core.windows.net/${azurerm_storage_container.blobstorage.name}/${azurerm_storage_blob.blobobject.name}"
}