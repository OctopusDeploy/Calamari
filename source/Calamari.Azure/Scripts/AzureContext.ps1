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

Write-Verbose "Azure context parameters: "
Write-Verbose "  Subscription ID:       $OctopusAzureSubscriptionId"
Write-Verbose "  Subscription name:     $OctopusAzureSubscriptionName"

Write-Verbose "Importing Windows Azure modules"

Import-Module $OctopusAzureModulePath

If ([System.Convert]::ToBoolean($OctopusUseServicePrincipal)) {
	$securePassword = ConvertTo-SecureString $OctopusAzureADPassword -AsPlainText -Force
	$creds = New-Object System.Management.Automation.PSCredential ($OctopusAzureADClientId, $securePassword)
	$azureProfile = New-AzureProfile -SubscriptionId $OctopusAzureSubscriptionId -StorageAccount $OctopusAzureStorageAccountName -Credential $creds -Tenant $OctopusAzureADTenantId -ServicePrincipal
} Else {
	Write-Verbose "Loading the management certificate"
	Add-Type -AssemblyName "System"
	$certificate = new-object System.Security.Cryptography.X509Certificates.X509Certificate2 -ArgumentList @($OctopusAzureCertificateFileName, $OctopusAzureCertificatePassword, ([System.Security.Cryptography.X509Certificates.X509KeyStorageFlags] "PersistKeySet", "Exportable"))
	$azureProfile = New-AzureProfile -SubscriptionId $OctopusAzureSubscriptionId -StorageAccount $OctopusAzureStorageAccountName -Certificate $certificate
} 

$azureProfile.Save(".\AzureProfile.json")

Select-AzureProfile -Profile $azureProfile | Out-Null


<#
if (!(Get-AzureSubscription -SubscriptionName $OctopusAzureSubscriptionName -ErrorAction SilentlyContinue)) {
	Write-Verbose "Setting up the Azure subscription"

	Set-AzureSubscription -SubscriptionName $OctopusAzureSubscriptionName -SubscriptionId $OctopusAzureSubscriptionId -Certificate $certificate
}

Select-AzureSubscription -SubscriptionName $OctopusAzureSubscriptionName
#>

Write-Verbose "Invoking target script $OctopusAzureTargetScript"

. $OctopusAzureTargetScript 