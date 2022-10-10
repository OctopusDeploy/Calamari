data "aws_eks_cluster" "default" {
  name = aws_eks_cluster.default.name
}

data "aws_eks_cluster_auth" "default" {
  name = aws_eks_cluster.default.name
}

provider "kubernetes" {
  alias                  = "aws"
  host                   = data.aws_eks_cluster.default.endpoint
  cluster_ca_certificate = base64decode(data.aws_eks_cluster.default.certificate_authority[0].data)
  token                  = data.aws_eks_cluster_auth.default.token
}

resource "local_sensitive_file" "kubeconfig" {
  content = templatefile("${path.module}/kubeconfig.tpl", {
    cluster_name = aws_eks_cluster.default.name,
    cluster_ca    = data.aws_eks_cluster.default.certificate_authority[0].data,
    endpoint     = data.aws_eks_cluster.default.endpoint,
  })
  filename = "./kubeconfig-${aws_eks_cluster.default.name}"
}

resource "kubernetes_config_map" "aws_auth" {
  provider = kubernetes.aws
  metadata {
    name      = "aws-auth"
    namespace = "kube-system"
  }
  data = {
    mapRoles = <<-EOT
      - rolearn: ${aws_iam_role.user.arn}
        username: system:node:{{EC2PrivateDNSName}}
        groups:
          - system:bootstrappers
          - system:nodes
      - rolearn: ${aws_iam_role.ec2.arn}
        username: system:node:{{EC2PrivateDNSName}}
        groups:
          - system:bootstrappers
          - system:nodes
EOT
    mapUsers = <<-EOT
      - userarn: ${aws_iam_user.default.arn}
        username: ${aws_iam_user.default.name}
        groups:
          - system:masters
EOT
  }
}
