data "aws_eks_cluster" "default" {
  name = "${var.static_resource_prefix}-eks"
}

data "aws_eks_cluster_auth" "default" {
  name = data.aws_eks_cluster.default.name
}

resource "aws_iam_instance_profile" "profile" {
  role = aws_iam_role.ec2.name
}

data "aws_iam_policy_document" "user" {
  statement {
    actions = [
      "sts:AssumeRole",
      "eks:ListClusters",
      "eks:ListTagsForResource",
      "eks:AccessKubernetesApi",
      "eks:DescribeCluster",
    ]
    effect    = "Allow"
    resources = ["*"]
  }
}

data "aws_iam_policy_document" "userRole" {
  statement {
    actions = [
      "sts:AssumeRole"
    ]
    principals {
      type        = "AWS"
      identifiers = [aws_iam_user.default.arn]
    }
    principals {
      type        = "AWS"
      identifiers = [aws_iam_role.ec2.arn]
    }
  }
}

data "aws_iam_policy_document" "ec2Role" {
  statement {
    actions = [
      "sts:AssumeRole",
    ]
    principals {
      type        = "Service"
      identifiers = ["ec2.amazonaws.com"]
    }
  }
}

resource "aws_iam_policy" "default" {
  path   = "/test/"
  policy = data.aws_iam_policy_document.user.json
}

resource "aws_iam_role" "ec2" {
  assume_role_policy  = data.aws_iam_policy_document.ec2Role.json
  managed_policy_arns = [aws_iam_policy.default.arn]
}

resource "aws_iam_role" "user" {
  assume_role_policy = data.aws_iam_policy_document.userRole.json
  managed_policy_arns = [
    aws_iam_policy.default.arn
  ]
}

data "aws_availability_zones" "available" {
}

resource "aws_iam_access_key" "default" {
  user = aws_iam_user.default.name
}

resource "aws_iam_user" "default" {
  name = "${random_pet.prefix.id}-test"
  path = "/test/"
}

data "aws_subnet" "default" {
  count = 2

  availability_zone = data.aws_availability_zones.available.names[count.index]
  cidr_block        = "10.0.${count.index}.0/24"
  vpc_id            = data.aws_vpc.default.id
}

data "aws_vpc" "default" {
  cidr_block = "10.0.0.0/16"
}