#!/bin/bash

Octopus_K8S_ClusterUrl=$(get_octopusvariable "Octopus.Action.Kubernetes.ClusterUrl")
Octopus_K8S_Namespace=$(get_octopusvariable "Octopus.Action.Kubernetes.Namespace")
Octopus_K8S_SkipTlsVerification=$(get_octopusvariable "Octopus.Action.Kubernetes.SkipTlsVerification")
Octopus_AccountType=$(get_octopusvariable "Octopus.Account.AccountType")
Octopus_K8S_KubectlExe=$(get_octopusvariable "Octopus.Action.Kubernetes.CustomKubectlExecutable")

function get_kubectl {
  if [[ -z $Octopus_K8S_KubectlExe ]]; then
    Octopus_K8S_KubectlExe="kubectl"
  else
    command -v $Octopus_K8S_KubectlExe &>/dev/null
    if [[ $? != 0 ]]; then
      echo >&2 "The custom kubectl location of $Octopus_K8S_KubectlExe does not exist";
      exit 1
    fi
    alias kubectl=$Octopus_K8S_KubectlExe
  fi
}

function setup_context {
  if [[ -z $Octopus_K8S_ClusterUrl ]]; then
    echo >&2  "Kubernetes cluster URL is missing"
    exit 1
  fi

  if [[ -z $Octopus_AccountType ]]; then
    echo >&2  "Kubernetes account is missing"
    exit 1
  fi

  if [[ -z $Octopus_K8S_Namespace ]]; then
    Octopus_K8S_Namespace="default"
  fi

  if [[ -z $Octopus_K8S_SkipTlsVerification ]]; then
    Octopus_K8S_SkipTlsVerification=true
  fi

  if [[ "$Octopus_AccountType" == "Token" ]]; then
    Octopus_K8S_Token=$(get_octopusvariable "Octopus.Account.Token")
    echo "Creating kubectl context to $Octopus_K8S_ClusterUrl using a Token"
    if [[ -z $Octopus_K8S_Token ]]; then
      echo >2 "Kubernetes authentication Token is missing"
      exit 1
    fi
	kubectl config set-credentials octouser --token=$Octopus_K8S_Token
  elif [[ "$Octopus_AccountType" == "UsernamePassword" ]]; then
	Octopus_K8S_Username=$(get_octopusvariable "Octopus.Account.Username")
    echo "Creating kubectl context to $Octopus_K8S_ClusterUrl using $Octopus_K8S_Username"
    kubectl config set-credentials octouser --username=$Octopus_K8S_Username --password=$(get_octopusvariable "Octopus.Account.Password")
  else
    echo >&2 "The account $Octopus_AccountType is currently not valid for kubectl contexts"
    exit 1
  fi

   kubectl config set-cluster octocluster --insecure-skip-tls-verify=$Octopus_K8S_SkipTlsVerification --server=$Octopus_K8S_ClusterUrl --namespace=$Octopus_K8S_Namespace
   kubectl config set-context octocontext --user=octouser --cluster=octocluster
   kubectl config use-context octocontext
}

function configure_kubectl_path {
  export KUBECONFIG=$(get_octopusvariable "Octopus.Action.Kubernetes.KubectlConfig")
  echo "Temporary kubectl config set to $KUBECONFIG"
}

function create_namespace {
	if [[ -n "$Octopus_K8S_Namespace" ]]; then
		kubectl get namespace $Octopus_K8S_Namespace > /dev/null 2>&1 || kubectl create namespace $Octopus_K8S_Namespace
	fi
}

echo "##octopus[stdout-verbose]"
get_kubectl
create_namespace
configure_kubectl_path
setup_context
echo $KUBECONFIG
echo "##octopus[stdout-verbose]"
echo "Invoking target script \"$(get_octopusvariable "OctopusKubernetesTargetScript")\" with $(get_octopusvariable "OctopusKubernetesTargetScriptParameters") parameters"
echo "##octopus[stdout-default]"

source $(get_octopusvariable "OctopusKubernetesTargetScript") $(get_octopusvariable "OctopusKubernetesTargetScriptParameters")