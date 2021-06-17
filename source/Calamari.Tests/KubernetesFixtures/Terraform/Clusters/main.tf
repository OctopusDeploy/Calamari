variable "tests_source_dir" {
  type = string
}

variable "test_namespace" {
  type = string
}

variable "aks_client_id" {
  type = string
}

variable "aks_client_secret" {
  type = string
  sensitive = true
}

resource "random_pet" "prefix" {}
