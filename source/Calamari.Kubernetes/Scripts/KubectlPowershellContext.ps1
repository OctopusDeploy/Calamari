## Octopus Kubernetes Context script
## --------------------------------------------------------------------------------------
##
## This script is configure the default kubectl context for this step.
##
## The script is passed the following parameters.
##
##   $OctopusKubernetesTargetScript = "..."
##   $OctopusKubernetesTargetScriptParameters = "..."
##   $OctopusKubernetesInsecure = "false"
##   $OctopusKubernetesServer = "https://..."
##   $OctopusKubernetesToken = "..."
##   $OctopusKubernetesUsername = "..."
##   $OctopusKubernetesPassword = "..."
##  $OctopusKubernetesNamespace
##   $OctopusKubernetesKubeCtlConfig = ".."

function SetupContext {
    if($OctopusKubernetesToken -ne $null) {
        Write-Verbose "Creating kubectl context to $OctopusKubernetesServer using Token"
        kubectl config set-credentials octouser --token=$OctopusKubernetesToken
    } else {
        Write-Verbose "Creating kubectl context to $OctopusKubernetesServer using Username $OctopusKubernetesUsername"
        kubectl config set-credentials octouser --username=$OctopusKubernetesUsername --password=$OctopusKubernetesPassword
    }
    if($OctopusKubernetesInsecure -eq $null) {
        $OctopusKubernetesInsecure = $false;
    }
    
    kubectl config set-cluster octocluster --insecure-skip-tls-verify=$OctopusKubernetesInsecure --server=$OctopusKubernetesServer --namespace=$OctopusKubernetesNamespace
    kubectl config set-context octocontext --user=octouser --cluster=octocluster
    kubectl config use-context octocontext
}

function ConfigureKubeCtlPath {
    $env:KUBECONFIG=$OctopusKubernetesKubeCtlConfig
    Write-Verbose "Temporary kubectl config set to $OctopusKubernetesKubeCtlConfig"
}

Write-Host "##octopus[stdout-verbose]"
ConfigureKubeCtlPath
SetupContext
Write-Host "##octopus[stdout-default]"


Write-Verbose "Invoking target script $OctopusKubernetesTargetScript with $OctopusKubernetesTargetScriptParameters parameters"

Invoke-Expression ". $OctopusKubernetesTargetScript $OctopusKubernetesTargetScriptParameters"