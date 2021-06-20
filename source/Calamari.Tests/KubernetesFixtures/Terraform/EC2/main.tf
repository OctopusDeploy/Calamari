variable "tests_source_dir" {
  type = string
}

variable "cluster_name" {
  type = string
}

variable "aws_region" {
  type = string
}

variable "aws_vpc_id" {
  type = string
}

variable "aws_subnet_id" {
  type = string
}

variable "aws_iam_instance_profile_name" {
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
  excludes         = ["terraform_working", "terraform_working_ec2", "terraform_working_gcom", "Tools"]
}

resource "random_pet" "prefix" {}
