## Octopus Azure Context script, version 1.0
## --------------------------------------------------------------------------------------
## 
## This script is used to load the Azure Powershell module and select the Azure subscription
##
## The script is passed the following parameters. 
##
##   $OctopusUseBundledAzureModules = "true"
##   $OctopusAzureModulePath = "....\Calamari\AzurePowershell\Azure.psd1"
##   $OctopusAzureTargetScript = "..."
##   $UseServicePrincipal = "false"
##   $OctopusAzureSubscriptionId = "..."
##   $OctopusAzureStorageAccountName = "..."
##   $OctopusAzureCertificateFileName = "...."
##   $OctopusAzureCertificatePassword = "...."
##   $OctopusAzureADTenantId = "...."
##   $OctopusAzureADClientId = "...."
##   $OctopusAzureADPassword = "...."

$AzureRMModules = @(
  "AzureRM.ApiManagement",
  "AzureRM.Automation",
  "AzureRM.Backup",
  "AzureRM.Batch",
  "AzureRM.Compute",
  "AzureRM.DataFactories",
  "AzureRM.DataLakeAnalytics",
  "AzureRM.DataLakeStore",
  "AzureRM.Dns",
  "AzureRM.HDInsight",
  "AzureRM.Insights",
  "AzureRM.KeyVault",
  "AzureRM.Network",
  "AzureRM.NotificationHubs",
  "AzureRM.OperationalInsights",
  "AzureRM.RecoveryServices",
  "AzureRM.RedisCache",
  "AzureRM.Resources",
  "AzureRM.SiteRecovery",
  "AzureRM.Sql",
  "AzureRM.Storage",
  "AzureRM.StreamAnalytics",
  "AzureRM.Tags",
  "AzureRM.TrafficManager",
  "AzureRM.UsageAggregates",
  "AzureRM.Websites"
)

if ([System.Convert]::ToBoolean($OctopusUseBundledAzureModules)) {
	Write-Verbose "Importing Azure Service Management PowerShell module"
	Import-Module -Name ([IO.Path]::Combine("$OctopusAzureModulePath", "ServiceManagement", "Azure")) 
}

If ([System.Convert]::ToBoolean($OctopusUseServicePrincipal)) {
	if ([System.Convert]::ToBoolean($OctopusUseBundledAzureModules)) {
		# Import Resource Manager modules
		Write-Verbose "Importing Azure Resource Manager PowerShell modules"
		$ResourceManagerModulePath = [IO.Path]::Combine("$OctopusAzureModulePath", "ResourceManager", "AzureResourceManager")
		Import-Module -Name ([IO.Path]::Combine($ResourceManagerModulePath, "AzureRM.Profile")) 
		Import-Module -Name ([IO.Path]::Combine($ResourceManagerModulePath, "Azure.Storage")) 
		$AzureRMModules | ForEach {
			Import-Module -Name ([IO.Path]::Combine($ResourceManagerModulePath, $_)) 
		} 
	}

	# Authenticate via Service Principal
	$securePassword = ConvertTo-SecureString $OctopusAzureADPassword -AsPlainText -Force
	$creds = New-Object System.Management.Automation.PSCredential ($OctopusAzureADClientId, $securePassword)
	Write-Verbose "Authenticating with Service Principal"
	Login-AzureRmAccount -Credential $creds -TenantId $OctopusAzureADTenantId -ServicePrincipal
	Set-AzureRmContext -SubscriptionId $OctopusAzureSubscriptionId -TenantId $OctopusAzureADTenantId  
} Else {
	# Authenticate via Management Certificate
	Write-Verbose "Loading the management certificate"
	Add-Type -AssemblyName "System"
	$certificate = new-object System.Security.Cryptography.X509Certificates.X509Certificate2 -ArgumentList @($OctopusAzureCertificateFileName, $OctopusAzureCertificatePassword, ([System.Security.Cryptography.X509Certificates.X509KeyStorageFlags] "PersistKeySet", "Exportable"))
	$azureProfile = New-AzureProfile -SubscriptionId $OctopusAzureSubscriptionId -StorageAccount $OctopusAzureStorageAccountName -Certificate $certificate
	$azureProfile.Save(".\AzureProfile.json")
	Select-AzureProfile -Profile $azureProfile | Out-Null
} 

Write-Verbose "Invoking target script $OctopusAzureTargetScript"

try {
	. $OctopusAzureTargetScript
} catch {
	# Warn if FIPS 140 compliance required when using Service Management SDK
	if ([System.Security.Cryptography.CryptoConfig]::AllowOnlyFipsAlgorithms -and ![System.Convert]::ToBoolean($OctopusUseServicePrincipal)) {
		Write-Warning "The Azure Service Management SDK is not FIPS 140 compliant. http://g.octopushq.com/FIPS"
	}
	
	throw
}