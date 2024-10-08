output "eks_client_id" {
  value = aws_iam_access_key.default.id
}

output "eks_secret_key" {
  value     = aws_iam_access_key.default.secret
  sensitive = true
}

output "eks_iam_role_arn" {
  value = data.aws_iam_role.iam_role_with_cluster_access.arn
}

output "eks_cluster_endpoint" {
  description = "Endpoint for EKS control plane."
  value       = data.aws_eks_cluster.default.endpoint
}

output "eks_cluster_ca_certificate" {
  value     = base64decode(data.aws_eks_cluster.default.certificate_authority[0].data)
  sensitive = true
}

output "eks_cluster_name" {
  description = "EKS name."
  value       = data.aws_eks_cluster.default.name
}

output "eks_cluster_arn" {
  description = "EKS ARN"
  value       = data.aws_eks_cluster.default.arn
}

output "aws_vpc_id" {
  value = one(data.aws_eks_cluster.default.vpc_config[*].vpc_id)
}

output "aws_subnet_id" {
  value = tolist(data.aws_eks_cluster.default.vpc_config[0].subnet_ids)[0]
}

output "aws_iam_instance_profile_name" {
  value = data.aws_iam_instance_profile.profile.name
}