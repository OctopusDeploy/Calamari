Import-Module "${PSScriptRoot}\helpers\azure.psm1"
Import-Module "${PSScriptRoot}\helpers\logging.psm1"
Import-Module "${PSScriptRoot}\helpers\octopus.psm1"

Function Invoke-SandboxCleanup {
    $isCleanup = $false

    $azureProperties = @{
        ApplicationId  = $OctopusParameters["AzureAccount.Client"]
        SubscriptionId = $OctopusParameters["AzureSubscriptionID"]
        ClientSecret   = $OctopusParameters["AzureAccount.Password"]
        TenantId       = $OctopusParameters["AzureAccount.TenantId"]
    }
    
    $octopusProperties = @{
        EnvironmentName = $OctopusParameters["Octopus.Environment.Name"]
    }

    $cleanupProperties = @{
        NoDeleteToleranceInHours = $OctopusParameters["NoDeleteToleranceInHours"]
        DeleteCadenceString      = $OctopusParameters["DeleteCadenceString"]
        DesiredDateFormat        = "yyyy-MM-dd HH:mm"
    }

    $allResourceGroups = Get-SandboxResourceForSubscription -AzureProperties $azureProperties -CleanupProperties $cleanupProperties -OctopusProperties $octopusProperties
   
    if ($null -eq $allResourceGroups) {
        Log -IncludeTimestamp $true -Message "The Sandbox subscription is empty, there are no resources to process."
        return @()
    }

    Show-OctopusArtifactDocument -ResourceGroups $allResourceGroups -Azureproperties $azureProperties

    $resourceGroupsToDelete = @($allResourceGroups | Where-Object {
            $_.CleanupAction.Equals("Delete");
        })

    if ($($octopusProperties["EnvironmentName"]).Contains("Cleanup")) {
        $isCleanup = $true
    }
    $isCleanup 
    if (!$isCleanup -and $resourceGroupsToDelete.Count -gt 0) {
        Log -IncludeTimestamp $true -Message "This script is running in NOTIFY mode and will only notify on future deletions" 
    }

    if ($isCleanup) {
        Log -IncludeTimestamp $true -Message "ATTENTION: This script is running in CLEANUP mode and will automatically proceed with resource deletion. The cleanup process will start now."

        if ($resourceGroupsToDelete.Count -gt 0) { 
            Start-ResourceGroupDeletion -ResourceGroupsToDelete $resourceGroupsToDelete `
                -OctopusEnvironment $OctopusParameters["Octopus.Environment.Name"] `
                -ConnectedSubscriptionName $OctopusParameters["AzureAccount.SubscriptionNumber"] `
                -SlackProperties $

        }
    }

    $allResourceGroups
}
