variable "my_var" {
  description = "the var passed in"
}

variable "my_var_txt" {
  description = "the var passed in from a text file"
}

output "my_output" {
  value = "${var.my_var}"
}

output "my_output_from_txt_file" {
  value = "${var.my_var_txt}"
}