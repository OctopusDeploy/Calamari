#!/bin/bash

Octopus_K8S_ClusterUrl=$(get_octopusvariable "Octopus.Action.Kubernetes.ClusterUrl")
Octopus_K8S_Namespace=$(get_octopusvariable "Octopus.Action.Kubernetes.Namespace")
Octopus_K8S_SkipTlsVerification=$(get_octopusvariable "Octopus.Action.Kubernetes.SkipTlsVerification")
Octopus_AccountType=$(get_octopusvariable "Octopus.Account.AccountType")
Octopus_K8S_KubectlExe=$(get_octopusvariable "Octopus.Action.Kubernetes.CustomKubectlExecutable")
Octopus_K8S_Client_Cert=$(get_octopusvariable "Octopus.Action.Kubernetes.ClientCertificate")
Octopus_K8S_Client_Cert_Pem=$(get_octopusvariable "${Octopus_K8S_Client_Cert}.CertificatePem")
Octopus_K8S_Client_Cert_Key=$(get_octopusvariable "${Octopus_K8S_Client_Cert}.PrivateKeyPem")
Octopus_K8S_Server_Cert=$(get_octopusvariable "Octopus.Action.Kubernetes.CertificateAuthority")
Octopus_K8S_Server_Cert_Pem=$(get_octopusvariable "${Octopus_K8S_Server_Cert}.CertificatePem")

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
    write_verbose "No namespace provded. Using default"
    Octopus_K8S_Namespace="default"
  fi

  if [[ -z $Octopus_K8S_SkipTlsVerification ]]; then
    Octopus_K8S_SkipTlsVerification=true
  fi

  kubectl config set-cluster octocluster --insecure-skip-tls-verify=$Octopus_K8S_SkipTlsVerification --server=$Octopus_K8S_ClusterUrl
  kubectl config set-context octocontext --user=octouser --cluster=octocluster --namespace=$Octopus_K8S_Namespace
  kubectl config use-context octocontext

  if [[ ! -z $Octopus_K8S_Client_Cert ]]; then
	if [[ -z $Octopus_K8S_Client_Cert_Pem ]]; then
	  echo 2> "Kubernetes client certificate does not include the certificate data"
      exit 1
	fi

	if [[ -z $Octopus_K8S_Client_Cert_Key ]]; then
	  echo 2> "Kubernetes client certificate does not include the private key data"
  	  exit 1
	fi

	echo $Octopus_K8S_Client_Cert_Key > octo-client-key.pem
	echo $Octopus_K8S_Client_Cert_Pem > octo-client-cert.pem

	kubectl config set-credentials octouser --client-certificate=octo-client-cert.pem
	kubectl config set-credentials octouser --client-key=octo-client-key.pem
  fi

  if [[ ! -z $Octopus_K8S_Server_Cert ]]; then
	if [[ -z $Octopus_K8S_Server_Cert_Pem ]]; then
	  echo 2> "Kubernetes server certificate does not include the certificate data"
	  exit 1
	fi

	echo $Octopus_K8S_Server_Cert_Pem > octo-server-cert.pem
	kubectl config set-cluster octocluster --certificate-authority=octo-server-cert.pem
  fi

  if [[ "$Octopus_AccountType" == "Token" ]]; then
    Octopus_K8S_Token=$(get_octopusvariable "Octopus.Account.Token")
    echo "Creating kubectl context to $Octopus_K8S_ClusterUrl (namespace $Octopus_K8S_Namespace) using a Token"
    if [[ -z $Octopus_K8S_Token ]]; then
      echo >2 "Kubernetes authentication Token is missing"
      exit 1
    fi
	kubectl config set-credentials octouser --token=$Octopus_K8S_Token
  elif [[ "$Octopus_AccountType" == "UsernamePassword" ]]; then
	Octopus_K8S_Username=$(get_octopusvariable "Octopus.Account.Username")
    echo "Creating kubectl context to $Octopus_K8S_ClusterUrl (namespace $Octopus_K8S_Namespace) using $Octopus_K8S_Username"
    kubectl config set-credentials octouser --username=$Octopus_K8S_Username --password=$(get_octopusvariable "Octopus.Account.Password")
  elif [[ "$Octopus_AccountType" == "AmazonWebServicesAccount" ]]; then
        # kubectl doesn't yet support exec authentication
		# https://github.com/kubernetes/kubernetes/issues/64751
		# so build this manually
		Octopus_K8S_ClusterName=$(get_octopusvariable "Octopus.Action.Kubernetes.ClusterName")
        echo "Creating kubectl context to $K8S_ClusterUrl using EKS cluster name $K8S_ClusterName"
		
		# The call to set-cluster above will create a file with empty users. We need to call
		# set-cluster first, because if we try to add the exec user first, set-cluster will
		# delete those settings. So we now delete the users line (the last line of the yaml file)
		# and add our own.

		#(Get-Content $env:KUBECONFIG) -replace 'users: \[\]', '' | Set-Content $env:KUBECONFIG

		# https://docs.aws.amazon.com/eks/latest/userguide/create-kubeconfig.html
		echo "users:" >> $KUBECONFIG
		echo "- name: octouser" >> $KUBECONFIG
		echo "  user:" >> $KUBECONFIG
		echo "    exec:" >> $KUBECONFIG
		echo "      apiVersion: client.authentication.k8s.io/v1alpha1" >> $KUBECONFIG
		echo "      command: heptio-authenticator-aws" >> $KUBECONFIG
		echo "      args:" >> $KUBECONFIG
		echo "        - \"token\"" >> $KUBECONFIG
		echo "        - \"-i\"" >> $KUBECONFIG
		echo "        - \"$K8S_ClusterName\"" >> $KUBECONFIG
  elif [[ -z $Octopus_K8S_Client_Cert ]]; then
    echo >&2 "The account $Octopus_AccountType is currently not valid for kubectl contexts"
    exit 1
  fi


}

function configure_kubectl_path {
  export KUBECONFIG=$(get_octopusvariable "Octopus.Action.Kubernetes.KubectlConfig")
  echo "Temporary kubectl config set to $KUBECONFIG"
}

function create_namespace {
	if [[ -n "$Octopus_K8S_Namespace" ]]; then
		kubectl get namespace $Octopus_K8S_Namespace > /dev/null 2>&1 
		if [[ $? -ne 0 ]]; then
			echo "##octopus[stdout-default]"
			kubectl create namespace $Octopus_K8S_Namespace
			echo "##octopus[stdout-verbose]"
		fi
		
	fi
}

echo "##octopus[stdout-verbose]"
get_kubectl
configure_kubectl_path
setup_context
create_namespace
echo $KUBECONFIG
echo "##octopus[stdout-verbose]"
echo "Invoking target script \"$(get_octopusvariable "OctopusKubernetesTargetScript")\" with $(get_octopusvariable "OctopusKubernetesTargetScriptParameters") parameters"
echo "##octopus[stdout-default]"

source $(get_octopusvariable "OctopusKubernetesTargetScript") $(get_octopusvariable "OctopusKubernetesTargetScriptParameters")