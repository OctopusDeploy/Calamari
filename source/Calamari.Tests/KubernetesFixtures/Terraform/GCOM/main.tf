variable "tests_source_dir" {
  type = string
}

resource "tls_private_key" "default" {
  algorithm = "RSA"
  rsa_bits  = 4096
}

data "archive_file" "data" {
  type             = "zip"
  source_dir       = var.tests_source_dir
  output_file_mode = "0666"
  output_path      = "data.zip"
  excludes = ["terraform_working", "Tools"]
}

resource "random_pet" "prefix" {}
