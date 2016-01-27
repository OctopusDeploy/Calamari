## 
## The script will be passed the following parameters in addition to the normal Octopus 
## variables passed to any PowerShell script. 
## 
##   $OctopusAzureServiceName              // The name of your cloud service
##   $OctopusAzureStorageAccountName       // The name of your storage account
##   $OctopusAzureSlot                     // The name of the slot to deploy to (Staging or Production)
##   $OctopusAzureDeploymentLabel          // The label to use for deployment
##   $OctopusAzureSwapIfPossible           // "True" if we should attempt to "swap" deployments rather than a new deployment
 
function SwapDeployment()
{
    Write-Verbose "Swapping the staging environment to production"
    Move-AzureDeployment -ServiceName $OctopusAzureServiceName
}

if (($OctopusAzureSwapIfPossible -eq $true) -and ($OctopusAzureSlot -eq "Production")) 
{
    Write-Verbose "Checking whether a swap is possible"
    $staging = Get-AzureDeployment -ServiceName $OctopusAzureServiceName -Slot "Staging" -ErrorVariable a -ErrorAction silentlycontinue
    if (($a[0] -ne $null) -or ($staging.Name -eq $null)) 
    {
        Write-Verbose "Nothing is deployed in staging"
    }
    else 
    {
        Write-Verbose ("Current staging deployment: " + $staging.Label)
        if ($staging.Label -eq $OctopusAzureDeploymentLabel) {
            SwapDeployment
			Set-OctopusVariable -name "OctopusAzureCloudServiceDeploymentSwapped" -value $true
        }
    }
}
