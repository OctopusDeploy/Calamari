data "aws_iam_role" "iam_role_with_cluster_access" {
  name = "${var.static_resource_prefix}-iam-role-with-cluster-access"
}

data "aws_eks_cluster" "default" {
  name = "${var.static_resource_prefix}-eks"
}

data "aws_iam_instance_profile" "profile" {
  name = "${var.static_resource_prefix}-instance-profile"
}

resource "aws_iam_user" "default" {
  name = "${var.static_resource_prefix}-${random_pet.name.id}"
  path = "/test/"
}

resource "aws_iam_access_key" "default" {
  user = aws_iam_user.default.name
}

data "aws_iam_policy_document" "user" {
  statement {
    effect = "Allow"
    actions = [
      "sts:AssumeRole"
    ]
    
    resources = [data.aws_iam_role.iam_role_with_cluster_access.arn]
  }
}

resource "aws_iam_policy" "user" {
  path   = "/test/"
  policy = data.aws_iam_policy_document.user.json
}

resource "aws_iam_user_policy_attachment" "default" {
  user       = aws_iam_user.default.name
  policy_arn = aws_iam_policy.user.arn
}
