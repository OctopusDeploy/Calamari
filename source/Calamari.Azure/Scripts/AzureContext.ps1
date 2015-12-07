## Octopus Azure Context script, version 1.0
## --------------------------------------------------------------------------------------
## 
## This script is used to load the Azure Powershell module and select the Azure subscription
##
## The script is passed the following parameters. 
##
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

Write-Verbose "Importing Azure PowerShell modules"
Import-Module $OctopusAzureModulePath

If ([System.Convert]::ToBoolean($OctopusUseServicePrincipal)) {
	$securePassword = ConvertTo-SecureString $OctopusAzureADPassword -AsPlainText -Force
	$creds = New-Object System.Management.Automation.PSCredential ($OctopusAzureADClientId, $securePassword)
	Write-Verbose "Authenticating with Service Principal"
	Login-AzureRmAccount -Credential $creds -TenantId $OctopusAzureADTenantId -ServicePrincipal
} Else {
	Write-Verbose "Loading the management certificate"
	Add-Type -AssemblyName "System"
	$certificate = new-object System.Security.Cryptography.X509Certificates.X509Certificate2 -ArgumentList @($OctopusAzureCertificateFileName, $OctopusAzureCertificatePassword, ([System.Security.Cryptography.X509Certificates.X509KeyStorageFlags] "PersistKeySet", "Exportable"))
	$azureProfile = New-AzureProfile -SubscriptionId $OctopusAzureSubscriptionId -StorageAccount $OctopusAzureStorageAccountName -Certificate $certificate
	$azureProfile.Save(".\AzureProfile.json")
	Select-AzureProfile -Profile $azureProfile | Out-Null
} 

Write-Verbose "Invoking target script $OctopusAzureTargetScript"

. $OctopusAzureTargetScript 