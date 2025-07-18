Function Invoke-NewSlackNotificationRequest {
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [Object[]]$Attachments,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        $SlackProperties
    )

    $header = @{
        'Accept'        = 'application/json';
        'Authorization' = "Bearer $($SlackProperties["SlackBearerToken"])";
        'Content-Type'  = 'application/json'
    }

    $payload = @{
        channel     = $SlackProperties["TeamSlackChannel"]
        attachments = $Attachments
    }

    $body = ($payload | ConvertTo-Json -Depth 4)

    Invoke-Restmethod -Method POST `
        -Uri "https://slack.com/api/chat.postMessage" `
        -Body $body `
        -Header $header
}

Function Add-SlackAttachmentField {
    param(
        [Object[]]$FieldItems,
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [bool]$WriteCleanupNotificationTag
    )

    $codeBlockCharacter = "``````"
    $formattedAttachment = $codeBlockCharacter
    $itemsCount = 0

    foreach ($item in $FieldItems) {
        if ($null -ne $item.ResourceGroupName) {
            $itemsCount++

            if ($WriteCleanupNotificationTag) {
                Set-CleanupNotificationIsSentTag -ResourceGroup $item
            }

            $ResourceGroup = "$($item.ResourceGroupName)" + "`n" + "   Created On: '" + "$($item.CreatedTime)'" + "`n" + "   Expired On: '" + "$($item.ExpirationDate)'" + "`n" 
            $formattedAttachment += $ResourceGroup;
        }
    }
    $formattedAttachment += $codeBlockCharacter
    return $formattedAttachment
}

Function Initialize-SlackNotification {
    param (
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [array]$ResourceGroupsToDelete,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        $SlackProperties,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        $OctopusProperties,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        $CleanupProperties,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        $AzureProperties
    )

    $slackAttachment = @{
        fallback    = ""
        color       = ""
        pretext     = ""
        author_name = ""
        author_link = ""
        title       = "Azure Sandbox Cleanup"
        title_link  = ""

        fields      = @()
    }

    $subscriptionName = (Get-AzSubscription -SubscriptionId $AzureProperties["SubscriptionId"]).Name

    if ($($OctopusProperties["EnvironmentName"]).Contains("Notify")) {
        $slackAttachment.pretext = ":warning: $($ResourceGroupsToDelete.Count) Azure Sandbox Resource groups in '$subscriptionName' scheduled for deletion on $($CleanupProperties["DeleteCadenceString"]). <removed|Tag your Sandbox resource to avoid deletion>"
        $slackAttachment.fallback = "Cleanup is scheduled to delete $($ResourceGroupsToDelete.Count) resource groups '$subscriptionName'"
        $slackAttachment.color = "#C27408"
    }
    else {
        $slackAttachment.pretext = ":recycle: $($ResourceGroupsToDelete.Count) Azure Sandbox Resource groups in '$subscriptionName' actively being deleted NOW). <removed|Tag your Sandbox resource to avoid deletion>"
        $slackAttachment.fallback = "Planned cleanup in Progress, Deleting $($ResourceGroupsToDelete.Count) groups in '$subscriptionName"
        $slackAttachment.color = "#195FB5"
    }

    $formattedAttachment = Add-SlackAttachmentField -FieldItems $ResourceGroupsToDelete -WriteCleanupNotificationTag $SlackProperties["WriteCleanupNotificationTag"]

    $slackAttachment.fields = @(
        @{
            value = $formattedAttachment
        }
    )

    Invoke-NewSlackNotificationRequest -Attachments $slackAttachment -SlackProperties $SlackProperties
}
