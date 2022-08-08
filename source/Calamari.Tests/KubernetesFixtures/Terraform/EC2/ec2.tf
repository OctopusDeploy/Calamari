data "aws_ami" "debian" {
  most_recent = true

  filter {
    name   = "name"
    values = ["debian-10-amd64-*"]
  }

  filter {
    name   = "virtualization-type"
    values = ["hvm"]
  }

  owners = ["136693071363"]
}

data "aws_eks_cluster" "default" {
  name = var.cluster_name
}

data "http" "myip" {
  url = "http://ipv4.icanhazip.com"
}

resource "aws_security_group" "ssh" {
  ingress {
    from_port   = 22
    to_port     = 22
    protocol    = "tcp"
    cidr_blocks = ["${chomp(data.http.myip.body)}/32"]
  }
  egress = [
    {
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
    },
  ]
  vpc_id = var.aws_vpc_id
}

resource "local_file" "private_key" {
  content  = tls_private_key.default.private_key_pem
  filename = "private_key"
}

resource "aws_key_pair" "generated_key" {
  key_name   = "${random_pet.prefix.id}-key"
  public_key = tls_private_key.default.public_key_openssh
}

resource "aws_instance" "default" {
  depends_on = [
    data.archive_file.data
  ]

  tags = {
    Name = "${random_pet.prefix.id}-ec2"
  }
  ami                         = data.aws_ami.debian.id
  instance_type               = "t3.micro"
  associate_public_ip_address = true
  key_name                    = aws_key_pair.generated_key.key_name
  iam_instance_profile        = var.aws_iam_instance_profile_name
  vpc_security_group_ids      = [aws_security_group.ssh.id]
  subnet_id                   = var.aws_subnet_id
}