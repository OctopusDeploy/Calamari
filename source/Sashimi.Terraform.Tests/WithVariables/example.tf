variable "my_var" {
  description = "the var passed in"
}

output "my_output" {
  value = "${var.my_var}"
}