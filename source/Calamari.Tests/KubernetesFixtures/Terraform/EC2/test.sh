wget https://packages.microsoft.com/config/debian/10/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y apt-transport-https unzip
sudo apt-get update
sudo apt-get install -y dotnet-sdk-3.1

export AWS_CLUSTER_URL=${endpoint}
export AWS_CLUSTER_NAME=${cluster_name}
export AWS_REGION=${region}
export AWS_IAM_ROLE_ARN=${iam_role_arn}
export AWS_CLUSTER_ARN=${cluster_arn}

mkdir tools
cd tools || exit

curl -L -o kubectl "https://dl.k8s.io/release/$(curl -L -s https://dl.k8s.io/release/stable.txt)/bin/linux/amd64/kubectl"
curl -L -o aws-iam-authenticator "https://github.com/kubernetes-sigs/aws-iam-authenticator/releases/download/v0.5.3/aws-iam-authenticator_0.5.3_linux_amd64"
curl -L -o aws "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip"
unzip -u awscliv2.zip
mv aws awscli
cp -r awscli/dist/*

chmod u+x aws-iam-authenticator
chmod u+x kubectl
chmod u+x aws/dist/aws

export PATH="$PATH:$(pwd)"

cd ..

mkdir tests
cd tests || exit
unzip /tmp/data.zip

dotnet test Calamari.Tests.dll --Tests AuthoriseWithAmazonEC2Role --logger "console;verbosity=detailed"

dotnet test Calamari.Tests.dll --Tests DiscoverKubernetesClusterWithEc2InstanceCredentialsAndIamRole --logger "console;verbosity=detailed"

dotnet test Calamari.Tests.dll --Tests DiscoverKubernetesClusterWithEc2InstanceCredentialsAndNoIamRole --logger "console;verbosity=detailed"
