## Octopus Kubernetes Context script
## --------------------------------------------------------------------------------------
##
## This script is used to configure the default kubectl context for this step.

function GetKubectl() {
	$Kubectl_Exe=$OctopusParameters["Octopus.Action.Kubernetes.CustomKubectlExecutable"]
	if ([string]::IsNullOrEmpty($Kubectl_Exe)) {
		$Kubectl_Exe = "kubectl"
	} else {
		$Custom_Exe_Exists = Test-Path $Kubectl_Exe -PathType Leaf
		if(-not $Custom_Exe_Exists) {
			Write-Error "The custom kubectl location of $Kubectl_Exe does not exist. See https://g.octopushq.com/KubernetesTarget for more information."
			Exit 1
		}
	}
	return $Kubectl_Exe;
}

$K8S_ClusterUrl=$OctopusParameters["Octopus.Action.Kubernetes.ClusterUrl"]
$K8S_Namespace=$OctopusParameters["Octopus.Action.Kubernetes.Namespace"]
$K8S_SkipTlsVerification=$OctopusParameters["Octopus.Action.Kubernetes.SkipTlsVerification"]
$K8S_AccountType=$OctopusParameters["Octopus.Account.AccountType"]	
$K8S_Namespace=$OctopusParameters["Octopus.Action.Kubernetes.Namespace"]
$K8S_Client_Cert = $OctopusParameters["Octopus.Action.Kubernetes.ClientCertificate"]
$K8S_Client_Cert_Pem = $OctopusParameters["$($K8S_Client_Cert).CertificatePem"]
$K8S_Client_Cert_Key = $OctopusParameters["$($K8S_Client_Cert).PrivateKeyPem"]
$K8S_Server_Cert = $OctopusParameters["Octopus.Action.Kubernetes.CertificateAuthority"]
$K8S_Server_Cert_Pem = $OctopusParameters["$($K8S_Server_Cert).CertificatePem"]
$Kubectl_Exe=GetKubectl

function SetupContext {	
	if($K8S_AccountType -ne "AzureServicePrincipal" -and [string]::IsNullOrEmpty($K8S_ClusterUrl)){
		Write-Error "Kubernetes cluster URL is missing"
		Exit 1
	}

	if([string]::IsNullOrEmpty($K8S_AccountType) -and [string]::IsNullOrEmpty($K8S_Client_Cert)){
		Write-Error "Kubernetes account type or certificate is missing"
		Exit 1
	}

	if([string]::IsNullOrEmpty($K8S_Namespace)){
		Write-Verbose "No namespace provded. Using default"
		$K8S_Namespace="default"
	}

	if([string]::IsNullOrEmpty($K8S_SkipTlsVerification)) {
        $K8S_SkipTlsVerification = $false;
    }

	if ((Get-Command $Kubectl_Exe -ErrorAction SilentlyContinue) -eq $null) {
		Write-Error "Could not find $Kubectl_Exe. Make sure kubectl is on the PATH. See https://g.octopushq.com/KubernetesTarget for more information."
		Exit 1
	}

	# When using an Azure account, use the az command line tool to build the
	# kubeconfig file.
	if($K8S_AccountType -eq "AzureServicePrincipal") {		
		$K8S_Azure_Resource_Group=$OctopusParameters["Octopus.Action.Kubernetes.AksClusterResourceGroup"]
		$K8S_Azure_Cluster=$OctopusParameters["Octopus.Action.Kubernetes.AksClusterName"]
		Write-Host "Creating kubectl context to AKS Cluster in resource group $K8S_Azure_Resource_Group called $K8S_Azure_Cluster (namespace $K8S_Namespace) using a AzureServicePrincipal"
		& az aks get-credentials --resource-group $K8S_Azure_Resource_Group --name $K8S_Azure_Cluster --file $env:KUBECONFIG
		& $Kubectl_Exe config set-context $K8S_Azure_Cluster --namespace=$K8S_Namespace
	} else {
		& $Kubectl_Exe config set-cluster octocluster --server=$K8S_ClusterUrl
		& $Kubectl_Exe config set-context octocontext --user=octouser --cluster=octocluster --namespace=$K8S_Namespace
		& $Kubectl_Exe config use-context octocontext

		if(-not [string]::IsNullOrEmpty($K8S_Client_Cert)) {
			if ([string]::IsNullOrEmpty($K8S_Client_Cert_Pem)) {
				Write-Error "Kubernetes client certificate does not include the certificate data"
				Exit 1
			}

			if ([string]::IsNullOrEmpty($K8S_Client_Cert_Key)) {
				Write-Error "Kubernetes client certificate does not include the private key data"
				Exit 1
			}

			$K8S_Client_Cert_Key_Encoded = $([Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes($K8S_Client_Cert_Key)))
			$K8S_Client_Cert_Pem_Encoded = $([Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes($K8S_Client_Cert_Pem)))

			# Don't leak the private key in the logs
			Set-OctopusVariable -name "$($K8S_Client_Cert).PrivateKeyPemBase64" -value $K8S_Client_Cert_Key_Encoded -sensitive

			& $Kubectl_Exe config set users.octouser.client-certificate-data $K8S_Client_Cert_Pem_Encoded
			& $Kubectl_Exe config set users.octouser.client-key-data $K8S_Client_Cert_Key_Encoded
		}

		if(-not [string]::IsNullOrEmpty($K8S_Server_Cert)) {
			if ([string]::IsNullOrEmpty($K8S_Server_Cert_Pem)) {
				Write-Error "Kubernetes server certificate does not include the certificate data"
				Exit 1
			}

			# Inline the certificate as base64 encoded data
			& $Kubectl_Exe config set clusters.octocluster.certificate-authority-data $([Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes($K8S_Server_Cert_Pem)))
		}
		else {
			& $Kubectl_Exe config set-cluster octocluster --insecure-skip-tls-verify=$K8S_SkipTlsVerification
		}

		if($K8S_AccountType -eq "Token") {
			Write-Host "Creating kubectl context to $K8S_ClusterUrl (namespace $K8S_Namespace) using a Token"
			$K8S_Token=$OctopusParameters["Octopus.Account.Token"]
			if([string]::IsNullOrEmpty($K8S_Token)) {
				Write-Error "Kubernetes authentication Token is missing"
				Exit 1
			}

			& $Kubectl_Exe config set-credentials octouser --token=$K8S_Token
		} elseif($K8S_AccountType -eq "UsernamePassword") {
			$K8S_Username=$OctopusParameters["Octopus.Account.Username"]
			Write-Host "Creating kubectl context to $K8S_ClusterUrl (namespace $K8S_Namespace) using Username $K8S_Username"
			& $Kubectl_Exe config set-credentials octouser --username=$K8S_Username --password=$($OctopusParameters["Octopus.Account.Password"])
		} elseif($K8S_AccountType -eq "AmazonWebServicesAccount") {
			# kubectl doesn't yet support exec authentication
			# https://github.com/kubernetes/kubernetes/issues/64751
			# so build this manually
			$K8S_ClusterName=$OctopusParameters["Octopus.Action.Kubernetes.EksClusterName"]
			Write-Host "Creating kubectl context to $K8S_ClusterUrl (namespace $K8S_Namespace) using EKS cluster name $K8S_ClusterName"
		
			# The call to set-cluster above will create a file with empty users. We need to call
			# set-cluster first, because if we try to add the exec user first, set-cluster will
			# delete those settings. So we now delete the users line (the last line of the yaml file)
			# and add our own.

			(Get-Content $env:KUBECONFIG) -replace 'users: \[\]', '' | Set-Content $env:KUBECONFIG

			# https://docs.aws.amazon.com/eks/latest/userguide/create-kubeconfig.html
			Add-Content -NoNewline -Path $env:KUBECONFIG -Value "users:`n"
			Add-Content -NoNewline -Path $env:KUBECONFIG -Value "- name: octouser`n"
			Add-Content -NoNewline -Path $env:KUBECONFIG -Value "  user:`n"
			Add-Content -NoNewline -Path $env:KUBECONFIG -Value "    exec:`n"
			Add-Content -NoNewline -Path $env:KUBECONFIG -Value "      apiVersion: client.authentication.k8s.io/v1alpha1`n"
			Add-Content -NoNewline -Path $env:KUBECONFIG -Value "      command: aws-iam-authenticator`n"
			Add-Content -NoNewline -Path $env:KUBECONFIG -Value "      args:`n"
			Add-Content -NoNewline -Path $env:KUBECONFIG -Value "        - `"token`"`n"
			Add-Content -NoNewline -Path $env:KUBECONFIG -Value "        - `"-i`"`n"
			Add-Content -NoNewline -Path $env:KUBECONFIG -Value "        - `"$K8S_ClusterName`""        
		}	
		elseif ([string]::IsNullOrEmpty($K8S_Client_Cert)) {

			Write-Error "Account Type $K8S_AccountType is currently not valid for kubectl contexts"
			Exit 1
		}  
	}
}

function ConfigureKubeCtlPath {
    $env:KUBECONFIG=$OctopusParameters["Octopus.Action.Kubernetes.KubectlConfig"]
    Write-Host "Temporary kubectl config set to $env:KUBECONFIG"
}

function CreateNamespace {
	if (-not [string]::IsNullOrEmpty($K8S_Namespace)) {
		
		try
		{
			# We need to continue if "kubectl get namespace" fails
			$backupErrorActionPreference = $script:ErrorActionPreference
			$script:ErrorActionPreference = "Continue"

			# Attempt to get the outputs. This will fail if none are defined.
			$outputResult = & $Kubectl_Exe get namespace $K8S_Namespace 2> $null
		}
		finally
		{
			# Restore the default setting
			$script:ErrorActionPreference = $backupErrorActionPreference

			if ($LASTEXITCODE -ne 0) {
				Write-Host "##octopus[stdout-default]"
				& $Kubectl_Exe create namespace $K8S_Namespace
				Write-Host "##octopus[stdout-verbose]"
			}
		}
	}
}

Write-Host "##octopus[stdout-verbose]"
ConfigureKubeCtlPath
SetupContext
CreateNamespace
Get-Content $env:KUBECONFIG
Write-Host "##octopus[stdout-default]"
Write-Verbose "Invoking target script $OctopusKubernetesTargetScript with $OctopusKubernetesTargetScriptParameters parameters"

Invoke-Expression ". `"$OctopusKubernetesTargetScript`" $OctopusKubernetesTargetScriptParameters"