variable "static_resource_prefix" {
  type    = string
  default = null
}

variable "aks_client_id" {
  type = string
}

variable "aks_client_secret" {
  type      = string
  sensitive = true
}