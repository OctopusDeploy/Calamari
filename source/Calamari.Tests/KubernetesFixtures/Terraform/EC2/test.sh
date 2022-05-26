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
# TODO: there is an issue with stable kubectl with the error: exec plugin: invalid apiVersion "client.authentication.k8s.io/v1alpha1"
# so a known working version has been hardcoded below (v1.23.6).
# curl -L -o kubectl "https://dl.k8s.io/release/$(curl -L -s https://dl.k8s.io/release/stable.txt)/bin/linux/amd64/kubectl"
curl -L -o kubectl "https://dl.k8s.io/release/v1.23.6/bin/linux/amd64/kubectl"
curl -L -o aws-iam-authenticator "https://github.com/kubernetes-sigs/aws-iam-authenticator/releases/download/v0.5.3/aws-iam-authenticator_0.5.3_linux_amd64"

chmod u+x aws-iam-authenticator
chmod u+x kubectl

export PATH="$PATH:$(pwd)"

cd ..

mkdir tests
cd tests || exit
unzip /tmp/data.zip

dotnet test Calamari.Tests.dll --Tests AuthoriseWithAmazonEC2Role,DiscoverKubernetesClusterWithEc2InstanceCredentialsAndIamRole,DiscoverKubernetesClusterWithEc2InstanceCredentialsAndNoIamRole --logger "console;verbosity=detailed" -- NUnit.NumberOfTestWorkers=1
