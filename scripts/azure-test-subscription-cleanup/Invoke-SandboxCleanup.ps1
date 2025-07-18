Import-Module "${PSScriptRoot}\helpers\azure.psm1"
Import-Module "${PSScriptRoot}\helpers\logging.psm1"
Import-Module "${PSScriptRoot}\helpers\octopus.psm1"
Import-Module "${PSScriptRoot}\helpers\slack.psm1"

Function Invoke-SandboxCleanup {
    $isCleanup = $false

    $azureProperties = @{
        ApplicationId = $OctopusParameters["AzureAccount.Client"]
        SubscriptionId = $OctopusParameters["AzureSubscriptionID"]
        ClientSecret = $OctopusParameters["AzureAccount.Password"]
    }
    
    $octopusProperties = @{
        EnvironmentName = $OctopusParameters["Octopus.Environment.Name"]
    }

    $slackProperties = @{
        SendSlackNotification = ($OctopusParameters["SendSlackNotification"] -eq "True")
        WriteCleanupNotificationTag = ($OctopusParameters["WriteCleanupNotificationTag"] -eq "True")
        SlackBearerToken = $OctopusParameters["SlackBearerToken"]
        TeamSlackChannel = $OctopusParameters["TeamSlackChannel"]
    }

    $cleanupProperties = @{
        NoDeleteToleranceInHours = $OctopusParameters["NoDeleteToleranceInHours"]
        DeleteCadenceString = $OctopusParameters["DeleteCadenceString"]
        DesiredDateFormat = "yyyy-MM-dd HH:mm"
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

    $notifiedResourceGroupsToDelete = @($resourceGroupsToDelete | Where-Object {
        $_.CleanupNotificationIsSent -eq "True"
    })
    
    $resourceGroupsSurvivedChoppingBlock = @($allResourceGroups | Where-Object {
        $_.CleanupAction.Equals("Ignore") -and $_.CleanupNotificationIsSent -eq "True" 
    })

    if ($($octopusProperties["EnvironmentName"]).Contains("Cleanup")) {
        $isCleanup = $true
    }
    $isCleanup 
    if (!$isCleanup -and $resourceGroupsToDelete.Count -gt 0) {
        Log -IncludeTimestamp $true -Message "This script is running in NOTIFY mode and will only notify on future deletions"

        if ($slackProperties["SendSlackNotification"]) {
            Initialize-SlackNotification -ResourceGroupsToDelete $resourceGroupsToDelete `
                -SlackProperties $slackProperties `
                -OctopusProperties $octopusProperties `
                -CleanupProperties $cleanupProperties `
                -AzureProperties $azureProperties

        } else {
            Log -IncludeTimestamp $true -Message "The slack notification has been suppressed because the octopus variable 'SendSlackNotification' evaluates to false."
        }    
    }

    if ($isCleanup) {
        Log -IncludeTimestamp $true -Message "ATTENTION: This script is running in CLEANUP mode and will automatically proceed with resource deletion. The cleanup process will start now."

        if ($notifiedResourceGroupsToDelete.Count -gt 0) {
            if ($slackProperties["SendSlackNotification"]) {
                Initialize-SlackNotification -ResourceGroupsToDelete $notifiedResourceGroupsToDelete `
                    -SlackProperties $slackProperties `
                    -OctopusProperties $octopusProperties `
                    -CleanupProperties $cleanupProperties `
                    -AzureProperties $azureProperties
            } else {
                Log -IncludeTimestamp $true -Message "The slack notification has been suppressed because the octopus variable 'SendSlackNotification' evaluates to false."
            }    

            Start-ResourceGroupDeletion -ResourceGroupsToDelete $notifiedResourceGroupsToDelete `
                -OctopusEnvironment $OctopusParameters["Octopus.Environment.Name"] `
                -ConnectedSubscriptionName $OctopusParameters["AzureAccount.SubscriptionNumber"] `
                -SlackProperties $slackProperties

        } else {
            Log -IncludeTimestamp $true -Message "The slack notification has been suppressed because there are no resource groups to delete."
        }

        if ($resourceGroupsSurvivedChoppingBlock.Count -gt 0) {
            Invoke-CleanupSurvivingResourceGroups -ResourceGroupsSurvivedChoppingBlock $resourceGroupsSurvivedChoppingBlock `
                -OctopusEnvironment $OctopusParameters["Octopus.Environment.Name"] `
                -ConnectedSubscriptionName $OctopusParameters["ConnectedSubscriptionName"]
        }
    }

    $allResourceGroups
}
