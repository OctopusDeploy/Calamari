variable "test_namespace" {
  type = string
}

variable "static_resource_prefix" {
  type = string
}

resource "random_pet" "prefix" {}
