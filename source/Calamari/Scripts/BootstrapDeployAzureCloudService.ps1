## Octopus Azure deployment bootstrap script, version 1.0
## --------------------------------------------------------------------------------------
## 
## This script is used to load the right Azure subscription and management certificates, 
## and then it calls the DeployToAzure.ps1 script. You shouldn't need to modify this 
## script. To customize the Azure deployment process, take a copy of DeployToAzure.ps1 and 
## add it to your NuGet package. 
##
## The sript is passed the following parameters. 
##
##   $OctopusAzureModulePath = "C:\Program Files (x86)\Microsoft SDKs\Windows Azure\PowerShell\Azure\Azure.psd1"
##   $OctopusAzureCertificateFileName = "...."
##   $OctopusAzureCertificatePassword = "...."
##   $OctopusAzureSubscriptionId = "..."
##   $OctopusAzureSubscriptionName = "..."
##   $OctopusAzureServiceName = "..."
##   $OctopusAzureStorageAccountName = "..."
##   $OctopusAzureSlot = "..."
##   $OctopusAzurePackageUri = "..."
##   $OctopusAzureConfigurationFile = "..."
##   $OctopusAzureDeploymentLabel = "..."
##   $OctopusAzureSwapIfPossible = "..."

Write-Host "Azure deployment parameters: "
Write-Host "  Subscription ID:       $OctopusAzureSubscriptionId"
Write-Host "  Subscription name:     $OctopusAzureSubscriptionName"
Write-Host "  Cloud service name:    $OctopusAzureServiceName"
Write-Host "  Storage account name:  $OctopusAzureStorageAccountName"
Write-Host "  Slot:                  $OctopusAzureSlot"
Write-Host "  Package URI:           $OctopusAzurePackageUri"
Write-Host "  Configuration file:    $OctopusAzureConfigurationFile"
Write-Host "  Deployment label:      $OctopusAzureDeploymentLabel"
Write-Host "  Allow swap:            $OctopusAzureSwapIfPossible"

Write-Host "Importing Windows Azure modules"

Import-Module $OctopusAzureModulePath

Write-Host "Loading the management certificate"

Add-Type -AssemblyName "System"
$certificate = new-object System.Security.Cryptography.X509Certificates.X509Certificate2 -ArgumentList @($OctopusAzureCertificateFileName, $OctopusAzureCertificatePassword)

Write-Host "Setting up the Azure subscription"

Set-AzureSubscription -CurrentStorageAccount $OctopusAzureStorageAccountName -SubscriptionName $OctopusAzureSubscriptionName -SubscriptionId $OctopusAzureSubscriptionId -Certificate $certificate

try
{
    Select-AzureSubscription -SubscriptionName $OctopusAzureSubscriptionName
    
    Write-Host "Starting the Azure deployment process"

    . .\DeployToAzure.ps1
}
finally
{
    Remove-AzureSubscription -SubscriptionName $OctopusAzureSubscriptionName -Force
}
