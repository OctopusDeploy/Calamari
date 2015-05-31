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

Write-Host "Azure context parameters: "
Write-Host "  Subscription ID:       $OctopusAzureSubscriptionId"
Write-Host "  Subscription name:     $OctopusAzureSubscriptionName"

Write-Host "Importing Windows Azure modules"

Import-Module $OctopusAzureModulePath

if (!(Get-AzureSubscription -SubscriptionName $OctopusAzureSubscriptionName)) {
	Write-Host "Loading the management certificate"

	Add-Type -AssemblyName "System"
	$certificate = new-object System.Security.Cryptography.X509Certificates.X509Certificate2 -ArgumentList @($OctopusAzureCertificateFileName, $OctopusAzureCertificatePassword)

	Write-Host "Setting up the Azure subscription"

	Set-AzureSubscription -SubscriptionName $OctopusAzureSubscriptionName -SubscriptionId $OctopusAzureSubscriptionId -Certificate $certificate
}

Select-AzureSubscription -SubscriptionName $OctopusAzureSubscriptionName

Write-Host "Invoking target script $OctopusAzureTargetScript"

. $OctopusAzureTargetScript 
