provider "aws" {
  assume_role {
    role_arn     = "arn:aws:iam::968802670493:role/e2e_buckets"
  }
}

variable "bucket_name" {
  description = "the bucket name to use"
}

resource "aws_s3_bucket" "mybucket" {
  bucket = "${var.bucket_name}"
  acl    = "private"
  cors_rule {
    allowed_headers = ["*"]
    allowed_methods = ["GET","PUT","HEAD","DELETE","POST"]
    allowed_origins = ["*"]
    max_age_seconds = 3000
  }
  tags {
    Name        = "My bucket"
    Environment = "Dev"
  }
}

resource "aws_s3_bucket_object" "test" {
  bucket = "${aws_s3_bucket.mybucket.bucket}"
  key = "test.txt"
  source = "test.txt"
  acl    = "public-read"
}

output "url" {
  value = "https://${aws_s3_bucket.mybucket.bucket_domain_name}/${aws_s3_bucket_object.test.key}"
}