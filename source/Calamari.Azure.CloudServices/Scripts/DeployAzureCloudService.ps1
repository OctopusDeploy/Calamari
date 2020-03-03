## Octopus Azure deployment script, version 1.0
## --------------------------------------------------------------------------------------
##
## This script is used to control how we deploy packages to Azure CloudServices. 
##
## When the script is run, the correct Azure subscription will ALREADY be selected,
## and we'll have loaded the neccessary management certificates. The Azure PowerShell module
## will also be loaded.  
##
## If you want to customize the Azure deployment process, simply copy this script into
## your deployment package as DeployToAzure.ps1. Octopus will invoke it instead of the default 
## script. 
## 
## The script will be passed the following parameters in addition to the normal Octopus 
## variables passed to any PowerShell script. 
## 
##   $OctopusAzureSubscriptionId           // The subscription ID GUID
##   $OctopusAzureServiceName              // The name of your cloud service
##   $OctopusAzureStorageAccountName       // The name of your storage account
##   $OctopusAzureSlot                     // The name of the slot to deploy to (Staging or Production)
##   $OctopusAzurePackageUri               // URI to the .cspkg file in Azure Blob Storage to deploy 
##   $OctopusAzureConfigurationFile        // The name of the Azure cloud service configuration file to use
##   $OctopusAzureDeploymentLabel          // The label to use for deployment
##   $OctopusAzureSwapIfPossible           // "True" if we should attempt to "swap" deployments rather than a new deployment

function CreateOrUpdate() 
{
    $deployment = Get-AzureDeployment -ServiceName $OctopusAzureServiceName -Slot $OctopusAzureSlot -ErrorVariable a -ErrorAction silentlycontinue
 
    if (($a[0] -ne $null) -or ($deployment.Name -eq $null)) 
    {
        CreateNewDeployment
        return
    } 
    
    UpdateDeployment
}
 
function UpdateDeployment()
{
    Write-Verbose "A deployment already exists in $OctopusAzureServiceName for slot $OctopusAzureSlot. Upgrading deployment..."
    Set-AzureDeployment -Upgrade -ServiceName $OctopusAzureServiceName -Package $OctopusAzurePackageUri -Configuration $OctopusAzureConfigurationFile -Slot $OctopusAzureSlot -Mode Auto -label $OctopusAzureDeploymentLabel -Force
}
 
function CreateNewDeployment()
{
    Write-Verbose "Creating a new deployment..."
    New-AzureDeployment -Slot $OctopusAzureSlot -Package $OctopusAzurePackageUri -Configuration $OctopusAzureConfigurationFile -label $OctopusAzureDeploymentLabel -ServiceName $OctopusAzureServiceName
}

function WaitForComplete() 
{
    $completeDeployment = Get-AzureDeployment -ServiceName $OctopusAzureServiceName -Slot $OctopusAzureSlot
    $completeDeploymentID = $completeDeployment.DeploymentId
    $completeDeploymentUrl = $completeDeployment.Url
        
    Set-OctopusVariable -name "OctopusAzureCloudServiceDeploymentID" -value $completeDeploymentID
    Set-OctopusVariable -name "OctopusAzureCloudServiceDeploymentUrl" -value $completeDeploymentUrl
    
    Write-Host "Deployment complete; Deployment ID: $completeDeploymentID"
}

CreateOrUpdate
WaitForComplete
