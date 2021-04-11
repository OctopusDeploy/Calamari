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
$K8S_OutputKubeConfig=$OctopusParameters["Octopus.Action.Kubernetes.OutputKubeConfig"]
$K8S_AccountType=$OctopusParameters["Octopus.Account.AccountType"]
$K8S_Namespace=$OctopusParameters["Octopus.Action.Kubernetes.Namespace"]
$K8S_Client_Cert = $OctopusParameters["Octopus.Action.Kubernetes.ClientCertificate"]
$EKS_Use_Instance_Role = $OctopusParameters["Octopus.Action.AwsAccount.UseInstanceRole"]
$K8S_Client_Cert_Pem = $OctopusParameters["$($K8S_Client_Cert).CertificatePem"]
$K8S_Client_Cert_Key = $OctopusParameters["$($K8S_Client_Cert).PrivateKeyPem"]
$K8S_Server_Cert = $OctopusParameters["Octopus.Action.Kubernetes.CertificateAuthority"]
$K8S_Server_Cert_Pem = $OctopusParameters["$($K8S_Server_Cert).CertificatePem"]
$Octopus_K8s_Server_Cert_Path = $OctopusParameters["Octopus.Action.Kubernetes.CertificateAuthorityPath"]
$Octopus_K8s_Pod_Service_Account_Token_Path = $OctopusParameters["Octopus.Action.Kubernetes.PodServiceAccountTokenPath"]
$IsUsingPodServiceAccount = $false
$Kubectl_Exe=GetKubectl

$OctopusAzureSubscriptionId = $OctopusParameters["Octopus.Action.Azure.SubscriptionId"]
$OctopusAzureADTenantId = $OctopusParameters["Octopus.Action.Azure.TenantId"]
$OctopusAzureADClientId = $OctopusParameters["Octopus.Action.Azure.ClientId"]
$OctopusAzureADPassword = $OctopusParameters["Octopus.Action.Azure.Password"]
$OctopusAzureEnvironment = $OctopusParameters["Octopus.Action.Azure.Environment"]
if ($null -eq $OctopusAzureEnvironment) {
	$OctopusAzureEnvironment = "AzureCloud"
}
$OctopusDisableAzureCLI = $OctopusParameters["OctopusDisableAzureCLI"]

function EnsureDirectoryExists([string] $path)
{
	New-Item -ItemType Directory -Force -Path $path *>$null
}

function ConnectAzAccount {
	# Authenticate via Service Principal
	$securePassword = ConvertTo-SecureString $OctopusAzureADPassword -AsPlainText -Force
	$creds = New-Object System.Management.Automation.PSCredential ($OctopusAzureADClientId, $securePassword)

	if(Get-Command "Login-AzureRmAccount" -ErrorAction SilentlyContinue)
	{
		# Turn off context autosave, as this will make all authentication occur in memory, and isolate each session from the context changes in other sessions
		Disable-AzureRMContextAutosave -Scope Process

		$AzureEnvironment = Get-AzureRmEnvironment -Name $OctopusAzureEnvironment
		if (!$AzureEnvironment)
		{
			Write-Error "No Azure environment could be matched given the name $OctopusAzureEnvironment"
			exit -2
		}

		Write-Verbose "AzureRM Modules: Authenticating with Service Principal"

		# Force any output generated to be verbose in Octopus logs.
		Write-Host "##octopus[stdout-verbose]"
		Login-AzureRmAccount -Credential $creds -TenantId $OctopusAzureADTenantId -SubscriptionId $OctopusAzureSubscriptionId -Environment $AzureEnvironment -ServicePrincipal
		Write-Host "##octopus[stdout-default]"
	}
	elseif (Get-InstalledModule Az -ErrorAction SilentlyContinue)
	{
		if (-Not(Get-Command "Disable-AzureRMContextAutosave" -errorAction SilentlyContinue))
		{
			# Turn on AzureRm aliasing
			# See https://docs.microsoft.com/en-us/powershell/azure/migrate-from-azurerm-to-az?view=azps-3.0.0#enable-azurerm-compatibility-aliases
			Enable-AzureRmAlias -Scope Process
		}

		# Turn off context autosave, as this will make all authentication occur in memory, and isolate each session from the context changes in other sessions
		Disable-AzContextAutosave -Scope Process

		$AzureEnvironment = Get-AzEnvironment -Name $OctopusAzureEnvironment
		if (!$AzureEnvironment)
		{
			Write-Error "No Azure environment could be matched given the name $OctopusAzureEnvironment"
			exit -2
		}

		Write-Verbose "Az Modules: Authenticating with Service Principal"

		# Force any output generated to be verbose in Octopus logs.
		Write-Host "##octopus[stdout-verbose]"
		Connect-AzAccount -Credential $creds -TenantId $OctopusAzureADTenantId -SubscriptionId $OctopusAzureSubscriptionId -Environment $AzureEnvironment -ServicePrincipal
		Write-Host "##octopus[stdout-default]"
	}

	If (!$OctopusDisableAzureCLI -or $OctopusDisableAzureCLI -like [Boolean]::FalseString) {
		try {
			# authenticate with the Azure CLI
			Write-Host "##octopus[stdout-verbose]"

			$env:AZURE_CONFIG_DIR = [System.IO.Path]::Combine($env:OctopusCalamariWorkingDirectory, "azure-cli")
			EnsureDirectoryExists($env:AZURE_CONFIG_DIR)

			$previousErrorAction = $ErrorActionPreference
			$ErrorActionPreference = "Continue"

			az cloud set --name $OctopusAzureEnvironment 2>$null 3>$null
			$ErrorActionPreference = $previousErrorAction

			Write-Host "Azure CLI: Authenticating with Service Principal"

			$loginArgs = @();
			$loginArgs += @("-u", (ConvertTo-QuotedString(ConvertTo-ConsoleEscapedArgument($OctopusAzureADClientId))));
			$loginArgs += @("-p", (ConvertTo-QuotedString(ConvertTo-ConsoleEscapedArgument($OctopusAzureADPassword))));
			$loginArgs += @("--tenant", (ConvertTo-QuotedString(ConvertTo-ConsoleEscapedArgument($OctopusAzureADTenantId))));
			az login --service-principal $loginArgs

			Write-Host "Azure CLI: Setting active subscription to $OctopusAzureSubscriptionId"
			az account set --subscription $OctopusAzureSubscriptionId

			Write-Host "##octopus[stdout-default]"
			Write-Verbose "Successfully authenticated with the Azure CLI"
		} catch  {
			# failed to authenticate with Azure CLI
			Write-Verbose "Failed to authenticate with Azure CLI"
			Write-Verbose $_.Exception.Message
		}
	}
}

function SetupContext {
	if($K8S_AccountType -ne "AzureServicePrincipal" -and [string]::IsNullOrEmpty($K8S_ClusterUrl)){
    Write-Error "Kubernetes cluster URL is missing"
    Exit 1
	}

	if([string]::IsNullOrEmpty($K8S_AccountType) -and [string]::IsNullOrEmpty($K8S_Client_Cert) -and $EKS_Use_Instance_Role -ine "true"){
	  if([string]::IsNullOrEmpty($Octopus_K8s_Pod_Service_Account_Token_Path) -and [string]::IsNullOrEmpty($Octopus_K8s_Server_Cert_Path)){
	    Write-Error "Kubernetes account type or certificate is missing"
    	Exit 1
	  }
		
		$Octopus_K8s_Pod_Service_Account_Token = Get-Content -Path $Octopus_K8s_Pod_Service_Account_Token_Path
		$Octopus_K8s_Server_Cert = Get-Content -Path $Octopus_K8s_Server_Cert_Path
		if([string]::IsNullOrEmpty($Octopus_K8s_Pod_Service_Account_Token)){
		  Write-Error "Pod service token file not found"
      Exit 1
		} elseif([string]::IsNullOrEmpty($Octopus_K8s_Server_Cert)){
      Write-Error "Certificate authority file not found"
      Exit 1
    } else {
      $IsUsingPodServiceAccount = $true
    }
	}

	if([string]::IsNullOrEmpty($K8S_Namespace)){
		Write-Verbose "No namespace provded. Using default"
		$K8S_Namespace="default"
	}

	if([string]::IsNullOrEmpty($K8S_SkipTlsVerification)) {
        $K8S_SkipTlsVerification = $false;
    }

	if([string]::IsNullOrEmpty($K8S_OutputKubeConfig)) {
        $K8S_OutputKubeConfig = $false;
    }

	if ((Get-Command $Kubectl_Exe -ErrorAction SilentlyContinue) -eq $null) {
		Write-Error "Could not find $Kubectl_Exe. Make sure kubectl is on the PATH. See https://g.octopushq.com/KubernetesTarget for more information."
		Exit 1
	}

	# When using an Azure account, use the az command line tool to build the
	# kubeconfig file.
	if($K8S_AccountType -eq "AzureServicePrincipal") {
		ConnectAzAccount

		$K8S_Azure_Resource_Group=$OctopusParameters["Octopus.Action.Kubernetes.AksClusterResourceGroup"]
		$K8S_Azure_Cluster=$OctopusParameters["Octopus.Action.Kubernetes.AksClusterName"]
		$K8S_Azure_Admin=$OctopusParameters["Octopus.Action.Kubernetes.AksAdminLogin"]
		Write-Host "Creating kubectl context to AKS Cluster in resource group $K8S_Azure_Resource_Group called $K8S_Azure_Cluster (namespace $K8S_Namespace) using a AzureServicePrincipal"
		if ([string]::IsNullOrEmpty($K8S_Azure_Admin) -or -not $K8S_Azure_Admin -ieq "true")
		{
			& az aks get-credentials --resource-group $K8S_Azure_Resource_Group --name $K8S_Azure_Cluster --file $env:KUBECONFIG --overwrite-existing
		}
		else
		{
			& az aks get-credentials --admin --resource-group $K8S_Azure_Resource_Group --name $K8S_Azure_Cluster --file $env:KUBECONFIG --overwrite-existing
			$K8S_Azure_Cluster += "-admin"
		}
		& $Kubectl_Exe config set-context $K8S_Azure_Cluster --namespace=$K8S_Namespace
	} elseif($IsUsingPodServiceAccount -eq $true) {
	  Write-Verbose "$Kubectl_Exe config set-cluster octocluster --server=$K8S_ClusterUrl --certificate-authority=$Octopus_K8s_Server_Cert_Path"
    & $Kubectl_Exe config set-cluster octocluster --server=$K8S_ClusterUrl --certificate-authority=$Octopus_K8s_Server_Cert_Path

    Write-Verbose "$Kubectl_Exe config set-context octocontext --user=octouser --cluster=octocluster --namespace=$K8S_Namespace"
    & $Kubectl_Exe config set-context octocontext --user=octouser --cluster=octocluster --namespace=$K8S_Namespace

    Write-Verbose "$Kubectl_Exe config use-context octocontext"
    & $Kubectl_Exe config use-context octocontext
    
    Write-Verbose "$Kubectl_Exe config set-credentials octouser --token=$Octopus_K8s_Pod_Service_Account_Token"
    & $Kubectl_Exe config set-credentials octouser --token=$Octopus_K8s_Pod_Service_Account_Token
	} else {
		Write-Verbose "$Kubectl_Exe config set-cluster octocluster --server=$K8S_ClusterUrl"
		& $Kubectl_Exe config set-cluster octocluster --server=$K8S_ClusterUrl

		Write-Verbose "$Kubectl_Exe config set-context octocontext --user=octouser --cluster=octocluster --namespace=$K8S_Namespace"
		& $Kubectl_Exe config set-context octocontext --user=octouser --cluster=octocluster --namespace=$K8S_Namespace

		Write-Verbose "$Kubectl_Exe config use-context octocontext"
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

			Write-Verbose "Encoding client cert key"
			$K8S_Client_Cert_Key_Encoded = $([Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes($K8S_Client_Cert_Key)))
			Write-Verbose "Encoding client cert pem"
			$K8S_Client_Cert_Pem_Encoded = $([Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes($K8S_Client_Cert_Pem)))

			# Don't leak the private key in the logs
			Set-OctopusVariable -name "$($K8S_Client_Cert).PrivateKeyPemBase64" -value $K8S_Client_Cert_Key_Encoded -sensitive

			Write-Verbose "$Kubectl_Exe config set users.octouser.client-certificate-data <client-cert-pem>"
			& $Kubectl_Exe config set users.octouser.client-certificate-data $K8S_Client_Cert_Pem_Encoded
			Write-Verbose "& $Kubectl_Exe config set users.octouser.client-key-data <client-cert-key>"
			& $Kubectl_Exe config set users.octouser.client-key-data $K8S_Client_Cert_Key_Encoded
		}

		if(-not [string]::IsNullOrEmpty($K8S_Server_Cert)) {
			if ([string]::IsNullOrEmpty($K8S_Server_Cert_Pem)) {
				Write-Error "Kubernetes server certificate does not include the certificate data"
				Exit 1
			}

			# Inline the certificate as base64 encoded data
			Write-Verbose "$Kubectl_Exe config set clusters.octocluster.certificate-authority-data <server-cert-pem>"
			& $Kubectl_Exe config set clusters.octocluster.certificate-authority-data $([Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes($K8S_Server_Cert_Pem)))
		}
		else {
			Write-Verbose "$Kubectl_Exe config set-cluster octocluster --insecure-skip-tls-verify=$K8S_SkipTlsVerification"
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
		} elseif($K8S_AccountType -eq "AmazonWebServicesAccount" -or $EKS_Use_Instance_Role -ieq "true") {
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
	# create an empty file, to suppress kubectl errors about the file missing
	Set-Content -Path $env:KUBECONFIG -Value ""
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
if ($K8S_OutputKubeConfig -eq $true) {
	& $Kubectl_Exe config view
}
Write-Verbose "Invoking target script $OctopusKubernetesTargetScript with $OctopusKubernetesTargetScriptParameters parameters"
Write-Host "##octopus[stdout-default]"

Invoke-Expression ". `"$OctopusKubernetesTargetScript`" $OctopusKubernetesTargetScriptParameters"