#!/bin/bash

Octopus_K8S_ClusterUrl=$(get_octopusvariable "Octopus.Action.Kubernetes.ClusterUrl")
Octopus_K8S_Namespace=$(get_octopusvariable "Octopus.Action.Kubernetes.Namespace")
Octopus_K8S_SkipTlsVerification=$(get_octopusvariable "Octopus.Action.Kubernetes.SkipTlsVerification")
Octopus_K8S_OutputKubeConfig=$(get_octopusvariable "Octopus.Action.Kubernetes.OutputKubeConfig")
Octopus_AccountType=$(get_octopusvariable "Octopus.Account.AccountType")
Octopus_K8S_KubectlExe=$(get_octopusvariable "Octopus.Action.Kubernetes.CustomKubectlExecutable")
Octopus_K8S_Client_Cert=$(get_octopusvariable "Octopus.Action.Kubernetes.ClientCertificate")
Octopus_EKS_Use_Instance_Role=$(get_octopusvariable "Octopus.Action.AwsAccount.UseInstanceRole")
Octopus_K8S_Client_Cert_Pem=$(get_octopusvariable "${Octopus_K8S_Client_Cert}.CertificatePem")
Octopus_K8S_Client_Cert_Key=$(get_octopusvariable "${Octopus_K8S_Client_Cert}.PrivateKeyPem")
Octopus_K8S_Server_Cert=$(get_octopusvariable "Octopus.Action.Kubernetes.CertificateAuthority")
Octopus_K8S_Server_Cert_Pem=$(get_octopusvariable "${Octopus_K8S_Server_Cert}.CertificatePem")
Octopus_K8s_Server_Cert_Path=$(get_octopusvariable "Octopus.Action.Kubernetes.CertificateAuthorityPath")
Octopus_K8s_Pod_Service_Account_Token_Path=$(get_octopusvariable "Octopus.Action.Kubernetes.PodServiceAccountTokenPath")
Octopus_K8s_Pod_Service_Account_Token=""
Octopus_K8s_Server_Cert_From_Path=""
IsUsingPodServiceAccount=false

function check_app_exists {
	command -v $1 > /dev/null 2>&1
	if [[ $? -ne 0 ]]; then
		echo >&2 "The executable $1 does not exist, or is not on the PATH."
		echo >&2 "See https://g.octopushq.com/KubernetesTarget for more information."
		exit 1
	fi
}

function get_kubectl {
  if [[ -z $Octopus_K8S_KubectlExe ]]; then
    Octopus_K8S_KubectlExe="kubectl"
  fi

  check_app_exists $Octopus_K8S_KubectlExe

  alias kubectl=$Octopus_K8S_KubectlExe
}

Octopus_Azure_Environment=$(get_octopusvariable "Octopus.Action.Azure.Environment")
Octopus_Azure_ADClientId=$(get_octopusvariable "Octopus.Action.Azure.ClientId")
Octopus_Azure_ADPassword=$(get_octopusvariable "Octopus.Action.Azure.Password")
Octopus_Azure_ADTenantId=$(get_octopusvariable "Octopus.Action.Azure.TenantId")
Octopus_Azure_SubscriptionId=$(get_octopusvariable "Octopus.Action.Azure.SubscriptionId")

function connect_az_account {
    {
        # authenticate with the Azure CLI
        echo "##octopus[stdout-verbose]"

        az cloud set --name ${Octopus_Azure_Environment:-"AzureCloud"} 2>null 3>null

        echo "Azure CLI: Authenticating with Service Principal"
        loginArgs=()
        # Use the full argument with an '=' because of https://github.com/Azure/azure-cli/issues/12105
        loginArgs+=("--username=$Octopus_Azure_ADClientId")
        loginArgs+=("--password=$Octopus_Azure_ADPassword")
        loginArgs+=("--tenant=$Octopus_Azure_ADTenantId")
        echo az login --service-principal ${loginArgs[@]}
        az login --service-principal ${loginArgs[@]}

        echo "Azure CLI: Setting active subscription to $Octopus_Azure_SubscriptionId"
        az account set --subscription $Octopus_Azure_SubscriptionId

        echo "##octopus[stdout-default]"
        write_verbose "Successfully authenticated with the Azure CLI"
    } || {
        # failed to authenticate with Azure CLI
        echo "Failed to authenticate with Azure CLI"
        exit 1
    }
}

function setup_context {
  if [[ "$Octopus_AccountType" != "AzureServicePrincipal" && -z $Octopus_K8S_ClusterUrl ]]; then
    echo >&2 "Kubernetes cluster URL is missing"
    exit 1
  fi
  
  if [[ -z $Octopus_AccountType && -z $Octopus_K8S_Client_Cert && ${Octopus_EKS_Use_Instance_Role,,} != "true" ]]; then
    if [[ -z $Octopus_K8s_Pod_Service_Account_Token_Path && -z $Octopus_K8s_Server_Cert_Path ]]; then
      echo >&2 "Kubernetes account type or certificate is missing"
      exit 1
    fi
    
    if [[ ! -z $Octopus_K8s_Pod_Service_Account_Token_Path ]]; then
      Octopus_K8s_Pod_Service_Account_Token=$(cat ${Octopus_K8s_Pod_Service_Account_Token_Path})
    fi
    if [[ ! -z $Octopus_K8s_Server_Cert_Path ]]; then
      Octopus_K8s_Server_Cert_From_Path=$(cat ${Octopus_K8s_Server_Cert_Path})
    fi
    
    if [[ -z $Octopus_K8s_Pod_Service_Account_Token ]]; then
      echo >&2 "Pod service token file not found"
      exit 1
    else
      IsUsingPodServiceAccount=true
    fi
  fi

  if [[ -z $Octopus_K8S_Namespace ]]; then
    write_verbose "No namespace provided. Using default"
    Octopus_K8S_Namespace="default"
  fi

  if [[ -z $Octopus_K8S_SkipTlsVerification ]]; then
    Octopus_K8S_SkipTlsVerification=true
  fi

  if [[ -z $Octopus_K8S_OutputKubeConfig ]]; then
    Octopus_K8S_OutputKubeConfig=false
  fi

  kubectl version --client=true
  if [[ $? -ne 0 ]]; then
	echo 2> "Could not find ${Octopus_K8S_KubectlExe}. Make sure kubectl is on the PATH."
	echo 2> "See https://g.octopushq.com/KubernetesTarget for more information."
	exit 1
  fi

  if [[ "$Octopus_AccountType" == "AzureServicePrincipal" ]]; then
    check_app_exists az
    connect_az_account
    K8S_Azure_Resource_Group=$(get_octopusvariable "Octopus.Action.Kubernetes.AksClusterResourceGroup")
    K8S_Azure_Cluster=$(get_octopusvariable "Octopus.Action.Kubernetes.AksClusterName")
    K8S_Azure_Admin=$(get_octopusvariable "Octopus.Action.Kubernetes.AksAdminLogin")
    echo "Creating kubectl context to AKS Cluster in resource group $K8S_Azure_Resource_Group called $K8S_Azure_Cluster (namespace $Octopus_K8S_Namespace) using a AzureServicePrincipal"
    if [[ -z $K8S_Azure_Admin || ${K8S_Azure_Admin,,} != "true" ]]; then
      az aks get-credentials --resource-group $K8S_Azure_Resource_Group --name $K8S_Azure_Cluster --file $KUBECONFIG --overwrite-existing
    else
      az aks get-credentials --admin --resource-group $K8S_Azure_Resource_Group --name $K8S_Azure_Cluster --file $KUBECONFIG --overwrite-existing
      K8S_Azure_Cluster+="-admin"
    fi
    kubectl config set-context $K8S_Azure_Cluster --namespace=$Octopus_K8S_Namespace
  elif [[ $IsUsingPodServiceAccount == "true" ]]; then
    kubectl config set-cluster octocluster --server=$Octopus_K8S_ClusterUrl
    
    if [[ -z $Octopus_K8s_Server_Cert_From_Path ]]; then
      kubectl config set-cluster octocluster --insecure-skip-tls-verify=$Octopus_K8S_SkipTlsVerification
    else
      kubectl config set-cluster octocluster --certificate-authority=$Octopus_K8s_Server_Cert_Path
    fi

    kubectl config set-context octocontext --user=octouser --cluster=octocluster --namespace=$Octopus_K8S_Namespace
    kubectl config use-context octocontext
    
    echo "Creating kubectl context to $Octopus_K8S_ClusterUrl (namespace $Octopus_K8S_Namespace) using Pod Service Account Token"
    kubectl config set-credentials octouser --token=$Octopus_K8s_Pod_Service_Account_Token
  else
    kubectl config set-cluster octocluster --server=$Octopus_K8S_ClusterUrl
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
  
      Octopus_K8S_Client_Cert_Pem_Encoded=$(echo "$Octopus_K8S_Client_Cert_Pem" | base64 $base64_args)
      Octopus_K8S_Client_Cert_Key_Encoded=$(echo "$Octopus_K8S_Client_Cert_Key" | base64 $base64_args)
  
      set_octopusvariable "${Octopus_K8S_Client_Cert}.PrivateKeyPemBase64" $Octopus_K8S_Client_Cert_Key_Encoded -sensitive
  
      kubectl config set users.octouser.client-certificate-data "$Octopus_K8S_Client_Cert_Pem_Encoded"
      kubectl config set users.octouser.client-key-data "$Octopus_K8S_Client_Cert_Key_Encoded"
    fi

    if [[ ! -z $Octopus_K8S_Server_Cert ]]; then
      if [[ -z $Octopus_K8S_Server_Cert_Pem ]]; then
        echo 2> "Kubernetes server certificate does not include the certificate data"
        exit 1
      fi
    
      Octopus_K8S_Server_Cert_Pem_Encoded=$(echo "$Octopus_K8S_Server_Cert_Pem" | base64 $base64_args)
      kubectl config set clusters.octocluster.certificate-authority-data "$Octopus_K8S_Server_Cert_Pem_Encoded"
    else
      kubectl config set-cluster octocluster --insecure-skip-tls-verify=$Octopus_K8S_SkipTlsVerification
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
    elif [[ "$Octopus_AccountType" == "AmazonWebServicesAccount" || ${Octopus_EKS_Use_Instance_Role,,} = "true" ]]; then
      # kubectl doesn't yet support exec authentication
      # https://github.com/kubernetes/kubernetes/issues/64751
      # so build this manually
      Octopus_K8S_ClusterName=$(get_octopusvariable "Octopus.Action.Kubernetes.EksClusterName")
      echo "Creating kubectl context to $Octopus_K8S_ClusterUrl using EKS cluster name $Octopus_K8S_ClusterName"

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
      echo "      command: aws-iam-authenticator" >> $KUBECONFIG
      echo "      args:" >> $KUBECONFIG
      echo "        - \"token\"" >> $KUBECONFIG
      echo "        - \"-i\"" >> $KUBECONFIG
      echo "        - \"$Octopus_K8S_ClusterName\"" >> $KUBECONFIG
    elif [[ -z $Octopus_K8S_Client_Cert ]]; then
      echo >&2 "The account $Octopus_AccountType is currently not valid for kubectl contexts"
      exit 1
    fi
  fi
}

function configure_kubectl_path {
  export KUBECONFIG=$(get_octopusvariable "Octopus.Action.Kubernetes.KubectlConfig")
  echo "Temporary kubectl config set to $KUBECONFIG"
  # create an empty file, to suppress kubectl errors about the file missing
  echo "" > $KUBECONFIG
  chmod u=rw,g=,o= $KUBECONFIG
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

function set_base64_args {
    # https://stackoverflow.com/questions/46463027/base64-doesnt-have-w-option-in-mac
    echo | base64 -w0 > /dev/null 2>&1
    if [ $? -eq 0 ]; then
      # GNU coreutils base64, '-w' supported
      base64_args='-w0'
    else
      # Openssl base64, no wrapping by default
      base64_args=''
    fi
}

echo "##octopus[stdout-verbose]"
check_app_exists base64
set_base64_args
get_kubectl
configure_kubectl_path
setup_context
create_namespace
if [[ "$Octopus_K8S_OutputKubeConfig" = true ]]; then
    kubectl config view
fi

OctopusKubernetesTargetScript=$(get_octopusvariable "OctopusKubernetesTargetScript")
echo "Invoking target script \"$OctopusKubernetesTargetScript\" with $(get_octopusvariable "OctopusKubernetesTargetScriptParameters") parameters"
echo "##octopus[stdout-default]"

source "$OctopusKubernetesTargetScript" $(get_octopusvariable "OctopusKubernetesTargetScriptParameters")