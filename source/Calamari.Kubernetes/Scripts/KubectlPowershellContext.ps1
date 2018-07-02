## Octopus Kubernetes Context script
## --------------------------------------------------------------------------------------
##
## This script is used to configure the default kubectl context for this step.

function SetupContext {
	$K8S_ClusterUrl=$OctopusParameters["Octopus.Action.Kubernetes.ClusterUrl"]
	$K8S_Namespace=$OctopusParameters["Octopus.Action.Kubernetes.Namespace"]
	$K8S_SkipTlsVerification=$OctopusParameters["Octopus.Action.Kubernetes.SkipTlsVerification"]
	$K8S_AccountType=$OctopusParameters["Octopus.Account.AccountType"]
	$Kubectl_Exe = $OctopusParameters["Octopus.Action.Kubernetes.CustomKubectlExecutable"]
	
	if([string]::IsNullOrEmpty($K8S_ClusterUrl)){
		Write-Error "Kubernetes cluster URL is missing"
		Exit 1
	}

	if([string]::IsNullOrEmpty($K8S_AccountType)){
		Write-Error "Kubernetes account type is missing"
		Exit 1
	}

	if([string]::IsNullOrEmpty($K8S_ServerUrl)){
		$K8S_Namespace="default"
	}

	 if([string]::IsNullOrEmpty($K8S_SkipTlsVerification)) {
        $K8S_SkipTlsVerification = $false;
    }

	if ([string]::IsNullOrEmpty($Kubectl_Exe)) {
		$Kubectl_Exe = "kubectl"
	} else {
		$Custom_Exe_Exists = Test-Path $Kubectl_Exe -PathType Leaf
		if(-not $Custom_Exe_Exists) {
			Write-Error "The custom kubectl location of $Kubectl_Exe does not exist"
			Exit 1
		}
	}

	& $Kubectl_Exe config set-cluster octocluster --insecure-skip-tls-verify=$K8S_SkipTlsVerification --server=$K8S_ClusterUrl --namespace=$K8S_Namespace
	& $Kubectl_Exe config set-context octocontext --user=octouser --cluster=octocluster
    & $Kubectl_Exe config use-context octocontext

    if($K8S_AccountType -eq "Token") {
        Write-Host "Creating kubectl context to $K8S_ClusterUrl using a Token"
		$K8S_Token=$OctopusParameters["Octopus.Account.Token"]
		if([string]::IsNullOrEmpty($K8S_Token)) {
			Write-Error "Kubernetes authentication Token is missing"
			Exit 1
		}

        & $Kubectl_Exe config set-credentials octouser --token=$K8S_Token
    } elseif($K8S_AccountType -eq "UsernamePassword") {
		$K8S_Username=$OctopusParameters["Octopus.Account.Username"]
        Write-Host "Creating kubectl context to $K8S_ClusterUrl using Username $K8S_Username"
        & $Kubectl_Exe config set-credentials octouser --username=$K8S_Username --password=$($OctopusParameters["Octopus.Account.Password"])
    } elseif($K8S_AccountType -eq "AmazonWebServicesAccount") {
		# kubectl doesn't yet support exec authentication
		# https://github.com/kubernetes/kubernetes/issues/64751
		# so build this manually
		$K8S_ClusterName=$OctopusParameters["Octopus.Action.Kubernetes.ClusterName"]
        Write-Host "Creating kubectl context to $K8S_ClusterUrl using EKS cluster name $K8S_ClusterName"

		Get-Content $env:KUBECONFIG
		
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
		Add-Content -NoNewline -Path $env:KUBECONFIG -Value "      command: heptio-authenticator-aws`n"
		Add-Content -NoNewline -Path $env:KUBECONFIG -Value "      args:`n"
		Add-Content -NoNewline -Path $env:KUBECONFIG -Value "        - `"token`"`n"
		Add-Content -NoNewline -Path $env:KUBECONFIG -Value "        - `"-i`"`n"
		Add-Content -NoNewline -Path $env:KUBECONFIG -Value "        - `"$K8S_ClusterName`""
        
    } else {
		Write-Error "Account Type $K8S_AccountType is currently not valid for kubectl contexts"
		Exit 1
	}      
}

function ConfigureKubeCtlPath {
    $env:KUBECONFIG=$OctopusParameters["Octopus.Action.Kubernetes.KubectlConfig"]
    Write-Host "Temporary kubectl config set to $env:KUBECONFIG"
}

Write-Host "##octopus[stdout-verbose]"
ConfigureKubeCtlPath
SetupContext
Write-Host "##octopus[stdout-default]"

$OctopusKubernetesTargetScript=$OctopusParameters["OctopusKubernetesTargetScript"]
$OctopusKubernetesTargetScriptParameters=$OctopusParameters["OctopusKubernetesTargetScriptParameters"]

Write-Verbose "Invoking target script $OctopusKubernetesTargetScript with $OctopusKubernetesTargetScriptParameters parameters"

Invoke-Expression ". $OctopusKubernetesTargetScript $OctopusKubernetesTargetScriptParameters"