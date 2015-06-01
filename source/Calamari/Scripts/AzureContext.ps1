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

if (!(Get-AzureSubscription -SubscriptionName $OctopusAzureSubscriptionName)) {
	Write-Verbose "Loading the management certificate"

	Add-Type -AssemblyName "System"
	$certificate = new-object System.Security.Cryptography.X509Certificates.X509Certificate2 -ArgumentList @($OctopusAzureCertificateFileName, $OctopusAzureCertificatePassword)

	Write-Verbose "Setting up the Azure subscription"

	Set-AzureSubscription -SubscriptionName $OctopusAzureSubscriptionName -SubscriptionId $OctopusAzureSubscriptionId -Certificate $certificate
}

Select-AzureSubscription -SubscriptionName $OctopusAzureSubscriptionName

Write-Verbose "Invoking target script $OctopusAzureTargetScript"

. $OctopusAzureTargetScript 

Select-AzureSubscription -NoCurrent
