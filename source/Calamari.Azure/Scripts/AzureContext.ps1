## Octopus Azure Context script, version 1.0
## --------------------------------------------------------------------------------------
## 
## This script is used to load the Azure Powershell module and select the Azure subscription
##
## The sript is passed the following parameters. 
##
##   $OctopusAzureModulePath = "....\Calamari\AzurePowershell\Azure.psd1"
##   $OctopusAzureCertificateFileName = "...."
##   $OctopusAzureCertificatePassword = "...."
##   $OctopusAzureSubscriptionId = "..."
##   $OctopusAzureSubscriptionName = "..."
##   $OctopusAzureTargetScript = "..."

Write-Verbose "Azure context parameters: "
Write-Verbose "  Subscription ID:       $OctopusAzureSubscriptionId"
Write-Verbose "  Subscription name:     $OctopusAzureSubscriptionName"

Write-Verbose "Importing Windows Azure modules"

Import-Module $OctopusAzureModulePath
function Get-OctopusAzureCertificate {
	return $certificate
}

Write-Verbose "Loading the management certificate"
Add-Type -AssemblyName "System"
$certificate = new-object System.Security.Cryptography.X509Certificates.X509Certificate2 -ArgumentList @($OctopusAzureCertificateFileName, $OctopusAzureCertificatePassword, ([System.Security.Cryptography.X509Certificates.X509KeyStorageFlags] "PersistKeySet", "Exportable"))
$azureProfile = New-AzureProfile -SubscriptionId $OctopusAzureSubscriptionId -Certificate $certificate

if (!(Test-Path ".\AzureProfile.json" -ErrorAction SilentlyContinue)) {
	$azureProfile.Save(".\AzureProfile.json")
}

Select-AzureProfile -Profile $azureProfile | Out-Null

if (!(Get-AzureSubscription -SubscriptionName $OctopusAzureSubscriptionName -ErrorAction SilentlyContinue)) {
	Write-Verbose "Setting up the Azure subscription"

	Set-AzureSubscription -SubscriptionName $OctopusAzureSubscriptionName -SubscriptionId $OctopusAzureSubscriptionId -Certificate $certificate
}

Select-AzureSubscription -SubscriptionName $OctopusAzureSubscriptionName

Write-Verbose "Invoking target script $OctopusAzureTargetScript"

. $OctopusAzureTargetScript 