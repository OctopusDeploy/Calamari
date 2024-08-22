resource "aws_security_group" "cluster" {
  ingress = [{
    cidr_blocks      = ["0.0.0.0/0"]
    description      = ""
    from_port        = 0
    ipv6_cidr_blocks = ["::/0"]
    prefix_list_ids  = []
    protocol         = "-1"
    security_groups  = []
    self             = false
    to_port          = 0
  }]
  egress = [{
    cidr_blocks = [
      "0.0.0.0/0",
    ]
    description = ""
    from_port   = 0
    ipv6_cidr_blocks = [
      "::/0",
    ]
    prefix_list_ids = []
    protocol        = "-1"
    security_groups = []
    self            = false
    to_port         = 0
  }]
  vpc_id = aws_vpc.default.id
}

resource "aws_iam_role" "nodes" {
  name = "${var.static_resource_prefix}-nodes"

  assume_role_policy = jsonencode({
    Statement = [{
      Action = "sts:AssumeRole"
      Effect = "Allow"
      Principal = {
        Service = "ec2.amazonaws.com"
      }
    }]
    Version = "2012-10-17"
  })
}

resource "aws_iam_role_policy_attachment" "example-AmazonEKSWorkerNodePolicy" {
  policy_arn = "arn:aws:iam::aws:policy/AmazonEKSWorkerNodePolicy"
  role       = aws_iam_role.nodes.name
}

resource "aws_iam_role_policy_attachment" "example-AmazonEKS_CNI_Policy" {
  policy_arn = "arn:aws:iam::aws:policy/AmazonEKS_CNI_Policy"
  role       = aws_iam_role.nodes.name
}

resource "aws_iam_role_policy_attachment" "example-AmazonEC2ContainerRegistryReadOnly" {
  policy_arn = "arn:aws:iam::aws:policy/AmazonEC2ContainerRegistryReadOnly"
  role       = aws_iam_role.nodes.name
}

resource "aws_eks_node_group" "default" {
  cluster_name    = aws_eks_cluster.default.name
  node_group_name = "${var.static_resource_prefix}-nodes"
  node_role_arn   = aws_iam_role.nodes.arn
  subnet_ids      = aws_subnet.default.*.id

  scaling_config {
    desired_size = 1
    max_size     = 1
    min_size     = 1
  }
}

resource "aws_eks_cluster" "default" {
  name     = "${var.static_resource_prefix}-eks"
  role_arn = aws_iam_role.cluster.arn
  version  = "1.28"

  tags = {
    octopus-environment = "Staging"
    octopus-role        = "discovery-role"
    source              = "calamari-e2e-tests"
  }

  vpc_config {
    endpoint_private_access = true
    public_access_cidrs     = ["0.0.0.0/0"]
    subnet_ids              = aws_subnet.default.*.id
    security_group_ids      = [aws_security_group.cluster.id]
  }
}

data "aws_iam_policy_document" "cluster" {
  statement {
    actions = [
      "sts:AssumeRole"
    ]
    principals {
      type        = "Service"
      identifiers = ["eks.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "cluster" {
  path               = "/test/"
  assume_role_policy = data.aws_iam_policy_document.cluster.json
  managed_policy_arns = [
    "arn:aws:iam::aws:policy/AmazonEKSClusterPolicy",
    "arn:aws:iam::aws:policy/AmazonEKSServicePolicy",
    "arn:aws:iam::aws:policy/AmazonEKSWorkerNodePolicy",
  ]
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

resource "aws_iam_instance_profile" "profile" {
  role = aws_iam_role.ec2.name
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

resource "aws_iam_policy" "default" {
  path   = "/test/"
  policy = data.aws_iam_policy_document.user.json
}

resource "aws_iam_user_policy_attachment" "default" {
  user       = aws_iam_user.default.name
  policy_arn = aws_iam_policy.default.arn
}

data "aws_availability_zones" "available" {
}

resource "aws_iam_access_key" "default" {
  user = aws_iam_user.default.name
}

resource "aws_iam_user" "default" {
  name = "${var.static_resource_prefix}-test"
  path = "/test/"
}

resource "aws_subnet" "default" {
  count = 2

  availability_zone       = data.aws_availability_zones.available.names[count.index]
  cidr_block              = "10.0.${count.index}.0/24"
  vpc_id                  = aws_vpc.default.id
  map_public_ip_on_launch = true
}

resource "aws_vpc" "default" {
  cidr_block           = "10.0.0.0/16"
  enable_dns_support   = true
  enable_dns_hostnames = true
}

resource "aws_internet_gateway" "default" {
  vpc_id = aws_vpc.default.id
}

resource "aws_route_table" "default" {
  vpc_id = aws_vpc.default.id

  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.default.id
  }
}

resource "aws_route_table_association" "default" {
  count = 2

  subnet_id      = aws_subnet.default[count.index].id
  route_table_id = aws_route_table.default.id
}
