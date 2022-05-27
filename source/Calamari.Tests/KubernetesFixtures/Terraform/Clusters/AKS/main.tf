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
