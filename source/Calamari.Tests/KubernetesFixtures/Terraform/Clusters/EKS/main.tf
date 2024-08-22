variable "tests_source_dir" {
  type = string
}

variable "static_resource_prefix" {
  type = string
}

resource "random_pet" "prefix" {}