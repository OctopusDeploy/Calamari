data "aws_eks_cluster_auth" "default" {
  name = data.aws_eks_cluster.default.name
}

provider "kubernetes" {
  host                   = data.aws_eks_cluster.default.endpoint
  cluster_ca_certificate = base64decode(data.aws_eks_cluster.default.certificate_authority[0].data)
  token                  = data.aws_eks_cluster_auth.default.token
}

resource "kubernetes_cluster_role" "default" {
  metadata {
    name = "${random_pet.prefix.id}-role-account"
  }

  rule {
    api_groups = ["*"]
    resources  = ["*"]
    verbs      = ["*"]
  }
}

resource "kubernetes_cluster_role_binding" "default" {
  metadata {
    name = "${random_pet.prefix.id}-role-account"
  }
  role_ref {
    api_group = "rbac.authorization.k8s.io"
    kind      = "ClusterRole"
    name      = kubernetes_cluster_role.default.metadata.0.name
  }
  subject {
    kind      = "User"
    name      = "system:node:${aws_instance.default.private_dns}"
    api_group = "rbac.authorization.k8s.io"
  }

  provisioner "remote-exec" {
    inline = [
      "chmod +x /tmp/script.sh",
      "/tmp/script.sh",
    ]
  }

  connection {
    type        = "ssh"
    user        = "admin"
    private_key = file(local_file.private_key.filename)
    host        = aws_instance.default.public_ip
  }
}