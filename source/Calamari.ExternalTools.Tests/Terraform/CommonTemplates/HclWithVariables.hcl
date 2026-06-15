variable stringvar {
  type = string
  default = "default string"
}
variable "images" {
  type = map(string)
  default = {
    us-east-1 = "image-1234"
    us-west-2 = "image-4567"
  }
}
variable "test2" {
  type = map
  default = {
    val1 = [
      "hi"]
  }
}
variable "test3" {
  type = map
  default = {
    val1 = {
      val2 = "#{RandomNumber}"
    }
  }
}
variable "test4" {
  type = map
  default = {
    val1 = {
      val2 = [
        "hi"]
    }
  }
}
# Example of getting an element from a list in a map
output "nestedlist" {
  value = "${element(var.test2["val1"], 0)}"
}
# Example of getting an element from a nested map
output "nestedmap" {
  value = "${lookup(var.test3["val1"], "val2")}"
}