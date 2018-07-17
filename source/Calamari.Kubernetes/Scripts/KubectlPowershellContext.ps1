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
			Write-Error "The custom kubectl location of $Kubectl_Exe does not exist"
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
$Kubectl_Exe=GetKubectl

function SetupContext {	
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
    }else {
		Write-Error "Account Type $K8S_AccountType is currently not valid for kubectl contexts"
		Exit 1
	}
   
    & $Kubectl_Exe config set-cluster octocluster --insecure-skip-tls-verify=$K8S_SkipTlsVerification --server=$K8S_ClusterUrl --namespace=$K8S_Namespace
    & $Kubectl_Exe config set-context octocontext --user=octouser --cluster=octocluster
    & $Kubectl_Exe config use-context octocontext
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
Write-Host "##octopus[stdout-default]"

Write-Verbose "Invoking target script $OctopusKubernetesTargetScript with $OctopusKubernetesTargetScriptParameters parameters"

Invoke-Expression ". `"$OctopusKubernetesTargetScript`" $OctopusKubernetesTargetScriptParameters"