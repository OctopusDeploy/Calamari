## Octopus Kubernetes Context script
## --------------------------------------------------------------------------------------
##
## This script is used to configure the default kubectl context for this step.

function SetupContext {
	$K8S_ClusterUrl=$OctopusParameters["Octopus.Action.Kubernetes.ClusterUrl"]
	$K8S_Namespace=$OctopusParameters["Octopus.Action.Kubernetes.Namespace"]
	$K8S_SkipTlsVerification=$OctopusParameters["Octopus.Action.Kubernetes.SkipTlsVerification"]
	$K8S_AccountType=$OctopusParameters["Octopus.Account.AccountType"]
	
	if([string]::IsNullOrEmpty($K8S_ClusterUrl)){
		Write-Error "Kubernetes cluster URL is missing"
		Exit 1
	}

	if([string]::IsNullOrEmpty($K8S_ServerUrl)){
		$K8S_Namespace="default"
	}

	 if([string]::IsNullOrEmpty($K8S_SkipTlsVerification)) {
        $K8S_SkipTlsVerification = $false;
    }

    if($K8S_AccountType -eq "Token") {
        Write-Host "Creating kubectl context to $K8S_ClusterUrl using a Token"
		$K8S_Token=$OctopusParameters["Octopus.Account.Token"]
		if([string]::IsNullOrEmpty($K8S_Token)) {
			Write-Error "Kubernetes authentication Token is missing"
			Exit 1
		}

        kubectl config set-credentials octouser --token=$K8S_Token
    } elseif($K8S_AccountType -eq "UsernamePassword") {
		$K8S_Username=$OctopusParameters["Octopus.Account.Username"]
        Write-Host "Creating kubectl context to $K8S_ClusterUrl using Username $K8S_Username"
        kubectl config set-credentials octouser --username=$K8S_Username --password=$($OctopusParameters["Octopus.Account.Password"])
    }else {
		Write-Error "Account Type $K8S_AccountType is currently not valid for kubectl contexts"
		Exit 1
	}

   
    
    kubectl config set-cluster octocluster --insecure-skip-tls-verify=$K8S_SkipTlsVerification --server=$K8S_ClusterUrl --namespace=$K8S_Namespace
    kubectl config set-context octocontext --user=octouser --cluster=octocluster
    kubectl config use-context octocontext
}

function ConfigureKubeCtlPath {
    $env:KUBECONFIG=$OctopusKubernetesKubeCtlConfig
    Write-Host "Temporary kubectl config set to $OctopusKubernetesKubeCtlConfig"
}

Write-Host "##octopus[stdout-verbose]"
ConfigureKubeCtlPath
SetupContext
Write-Host "##octopus[stdout-default]"


Write-Verbose "Invoking target script $OctopusKubernetesTargetScript with $OctopusKubernetesTargetScriptParameters parameters"

Invoke-Expression ". $OctopusKubernetesTargetScript $OctopusKubernetesTargetScriptParameters"