provider "azurerm" {
  features {}
}

variable "app_name" {
  description = "The name of the app"
}

variable "resource_group_name" {
  type = string
}

variable "resource_group_location" {
  type = string
}

resource "random_pet" "prefix" {}

resource "azurerm_resource_group" "resgrp" {
  name     = var.resource_group_name
  location = var.resource_group_location
  
  tags = {
    LifetimeInDays = 1
    source = "calamari-e2e-tests"
  }
}

resource "azurerm_app_service_plan" "service_plan" {
  name                = "${random_pet.prefix.id}-plan"
  location            = azurerm_resource_group.resgrp.location
  resource_group_name = azurerm_resource_group.resgrp.name

  sku {
    tier = "Standard"
    size = "S1"
  }
}

resource "azurerm_app_service" "web_app" {
  name                = var.app_name
  location            = azurerm_resource_group.resgrp.location
  resource_group_name = azurerm_resource_group.resgrp.name
  app_service_plan_id = azurerm_app_service_plan.service_plan.id

  site_config {
    dotnet_framework_version = "v4.0"
    scm_type                 = "LocalGit"
  }

  app_settings = {
    "SOME_KEY" = "some-value"
  }
}

output "url" {
  value = azurerm_app_service.web_app.default_site_hostname
}