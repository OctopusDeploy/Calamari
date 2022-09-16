output "ec2_address" {
  value = aws_instance.default.public_dns
}
